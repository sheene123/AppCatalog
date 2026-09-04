# syntax=docker/dockerfile:1

# --- Étape 1 : build ---
# On compile avec le SDK complet, puis on ne garde que le résultat publié.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copier d'abord les .csproj et restaurer : tant que les dépendances ne changent
# pas, Docker réutilise cette couche depuis le cache (build plus rapides).
COPY AppCatalog.sln ./
COPY src/AppCatalog.Api/AppCatalog.Api.csproj src/AppCatalog.Api/
COPY tests/AppCatalog.Api.Tests/AppCatalog.Api.Tests.csproj tests/AppCatalog.Api.Tests/
RUN dotnet restore src/AppCatalog.Api/AppCatalog.Api.csproj

# Puis le reste du code, et on publie en Release.
COPY . .
RUN dotnet publish src/AppCatalog.Api/AppCatalog.Api.csproj -c Release -o /app --no-restore

# --- Étape 2 : runtime ---
# Image ASP.NET Core seule (sans le SDK) : plus légère et moins de surface d'attaque.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app ./

# Le conteneur écoute en HTTP sur 8080 (défaut des images .NET 8).
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# Sonde de disponibilité exposée sur /health ; à brancher côté orchestrateur
# (Docker Compose, Kubernetes) qui redémarre le conteneur s'il ne répond plus.
ENTRYPOINT ["dotnet", "AppCatalog.Api.dll"]
