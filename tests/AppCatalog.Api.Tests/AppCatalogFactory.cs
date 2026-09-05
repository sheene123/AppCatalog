using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.Neo4j;

namespace AppCatalog.Api.Tests;

/// <summary>
/// Démarre l'API réelle branchée sur un vrai Neo4j éphémère (conteneur Testcontainers).
/// Un seul conteneur pour toute la classe de tests (IClassFixture) : rapide, et les
/// tests sont écrits pour être indépendants de l'état partagé (chacun crée/nettoie
/// ses propres données).
/// </summary>
public class AppCatalogFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly Neo4jContainer _neo4j = new Neo4jBuilder()
        .WithImage("neo4j:5.24")
        .Build();

    public async Task InitializeAsync() => await _neo4j.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _neo4j.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Schéma bolt:// (connexion directe) plutôt que neo4j:// (routing) :
            // avec le routing, Neo4j annonce son adresse interne au conteneur,
            // injoignable depuis l'hôte -> échec de connexion.
            var uri = _neo4j.GetConnectionString().Replace("neo4j://", "bolt://");
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Neo4j:Uri"] = uri,
                ["Neo4j:User"] = "neo4j",
                ["Neo4j:Password"] = "" // module sans auth -> AuthTokens.None côté API
            });
        });
    }
}
