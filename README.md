# AppCatalog

API REST de **référencement des applications d'un SI** : chaque application est
décrite par son nom, son propriétaire, sa pile technique, sa criticité et la date de
son dernier déploiement.

Le projet a été construit en **développement assisté par IA sur une stack .NET**
découverte pour l'occasion. L'objectif n'est pas de prouver une expertise .NET, mais
de montrer une **méthode d'industrialisation** : conventions partagées, tests comme
contrat, CI comme barrière. Voir [`JOURNAL.md`](JOURNAL.md) et
[`.github/copilot-instructions.md`](.github/copilot-instructions.md).

## Stack

| Élément | Choix |
|---|---|
| Runtime | ASP.NET Core 8 (C#), controllers |
| Persistance | EF Core 8 + SQLite, migrations |
| Doc API | Swagger / OpenAPI |
| Tests | xUnit + `WebApplicationFactory` (intégration, base SQLite in-memory) |
| Conteneur | Docker multi-étapes |
| CI | GitHub Actions : format, build, tests, audit de vulnérabilités |

## Lancer en local

```bash
dotnet run --project src/AppCatalog.Api
```

Au démarrage, l'API applique la migration EF Core et crée `appcatalog.db` avec 3
applications de démonstration. Puis :

- Swagger : http://localhost:5080/swagger (adapter le port affiché dans la console)
- Santé : `GET /health`

## Endpoints

| Méthode | Route | Rôle |
|---|---|---|
| GET | `/api/applications` | Liste |
| GET | `/api/applications/{id}` | Détail (404 si absent) |
| POST | `/api/applications` | Création (201 + `Location`) |
| PUT | `/api/applications/{id}` | Mise à jour |
| DELETE | `/api/applications/{id}` | Suppression (204) |
| GET | `/health` | Disponibilité |

Exemple :

```bash
curl -X POST http://localhost:5080/api/applications \
  -H "Content-Type: application/json" \
  -d '{"name":"Portail Voyageurs","owner":"DSI","stack":"ASP.NET Core","criticality":"Vital"}'
```

## Tests

```bash
dotnet test
```

Les tests démarrent l'API réelle en mémoire et vérifient le contrat HTTP de bout en
bout (CRUD, validation, codes de statut, santé).

## Docker

```bash
docker build -t appcatalog .
docker run -p 8080:8080 appcatalog
# -> http://localhost:8080/swagger
```

## Déploiement Azure (AKS + Terraform + CI/CD)

Infrastructure décrite en **Terraform** (`infra/terraform/`), déploiement sur **AKS**
via **GitHub Actions** en **OIDC sans secret**.

> ⚠️ **Coût** : un cluster AKS facture ses nœuds (~quelques €/jour pour 1 × B2s ;
> le control plane est en tier *Free*). Fais `terraform destroy` après la démo.

**1. Provisionner l'infra** (crée Resource Group + ACR + AKS) :

```bash
az login
cd infra/terraform
terraform init
terraform apply            # RG, ACR (Basic), AKS (1 nœud B2s, control plane Free)
```

Le rôle `AcrPull` est attribué à l'identité managée d'AKS : le cluster tire les
images **sans mot de passe de registre**.

**2. Câbler GitHub → Azure en OIDC** (aucun secret stocké) :

```bash
cd ../..                                   # racine du repo
GITHUB_REPO="sheene123/AppCatalog" ./scripts/bootstrap-azure-oidc.sh
```

Le script affiche les **Variables** à coller dans GitHub
(*Settings > Secrets and variables > Actions > Variables*).

**3. Déployer** : un push sur `main` déclenche `CI` puis `CD`
([.github/workflows/cd.yml](.github/workflows/cd.yml)), qui :
build l'image dans ACR (`az acr build`) → configure `kubectl` → `kubectl apply -k k8s/`
→ attend le `rollout`. L'IP publique s'affiche en fin de job (`kubectl get service`).

**Déployer à la main** (sans la CD) :

```bash
az aks get-credentials -g <rg> -n <aks> --admin
az acr build --registry <acr> --image appcatalog:demo .
kubectl apply -k k8s/
kubectl set image deployment/appcatalog api=<acr>.azurecr.io/appcatalog:demo -n appcatalog
kubectl rollout status deployment/appcatalog -n appcatalog
kubectl get service appcatalog -n appcatalog   # -> IP publique
```

**Détruire** (pour arrêter les frais) :

```bash
cd infra/terraform && terraform destroy
```

Les manifestes Kubernetes (`k8s/`) sont **durcis** : exécution `runAsNonRoot`,
système de fichiers en lecture seule, capacités Linux supprimées, quotas CPU/mémoire,
sondes liveness/readiness sur `/health`.

## Structure

```
AppCatalog/
├── src/AppCatalog.Api/
│   ├── Domain/          # entité Application, enum Criticality
│   ├── Data/            # AppCatalogDbContext (+ seed), Migrations/
│   ├── Contracts/       # DTO requête/réponse + mapping
│   ├── Controllers/     # ApplicationsController (CRUD)
│   └── Program.cs       # composition : DI, pipeline, migrations au démarrage
├── tests/AppCatalog.Api.Tests/
│   ├── AppCatalogFactory.cs      # API in-memory pour les tests
│   └── ApplicationsApiTests.cs
├── infra/terraform/              # IaC : Resource Group, ACR, AKS (+ rôle AcrPull)
├── k8s/                          # manifestes durcis : Deployment, Service, HPA, Namespace
├── scripts/bootstrap-azure-oidc.sh   # câblage OIDC GitHub -> Azure (sans secret)
├── Dockerfile
├── .editorconfig                 # conventions C# (vérifiées en CI)
├── .github/
│   ├── workflows/ci.yml          # format, build, tests, audit CVE
│   ├── workflows/cd.yml          # build ACR + déploiement AKS (OIDC)
│   └── copilot-instructions.md   # règles imposées au code généré par IA
├── JOURNAL.md                    # journal d'industrialisation (méthode + erreurs)
└── README.md
```

## Périmètre volontairement limité

Pas de frontend, pas d'authentification réelle, pas de Kubernetes. La discipline de
périmètre est un choix : un artefact petit et entièrement maîtrisé vaut mieux qu'un
projet large dont une partie échappe. Les extensions envisagées sont listées dans
[`JOURNAL.md`](JOURNAL.md).
