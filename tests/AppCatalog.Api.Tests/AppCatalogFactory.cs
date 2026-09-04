using AppCatalog.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AppCatalog.Api.Tests;

/// <summary>
/// Démarre l'API réelle en mémoire pour les tests d'intégration.
/// On remplace la base SQLite fichier par une base SQLite « in-memory » :
/// mêmes requêtes, même EF Core, mais rien à installer et tout est jetable.
///
/// La connexion in-memory est gardée ouverte pendant toute la vie de la factory :
/// dès qu'on la ferme, SQLite détruit la base. On repart donc d'une base propre
/// (schéma + données de seed) à chaque nouvelle factory, ce qui isole les tests.
/// </summary>
public class AppCatalogFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Environnement « Testing » : Program.cs saute alors le Migrate() de prod.
        builder.UseEnvironment("Testing");

        _connection.Open();

        builder.ConfigureTestServices(services =>
        {
            // On retire l'enregistrement DbContext de prod (SQLite fichier)...
            services.RemoveAll<DbContextOptions<AppCatalogDbContext>>();
            // ...et on le remplace par la connexion in-memory partagée.
            services.AddDbContext<AppCatalogDbContext>(o => o.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Crée le schéma et insère les données de seed dans la base in-memory.
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppCatalogDbContext>();
        db.Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose(); // ferme la connexion -> détruit la base in-memory
    }
}
