using AppCatalog.Api.Domain;
using Neo4j.Driver;

namespace AppCatalog.Api.Data;

/// <summary>
/// Au démarrage : attend que Neo4j réponde, garantit la contrainte d'unicité sur
/// l'id, et insère un petit graphe de démonstration si la base est vide.
/// Exécuté comme service hébergé (IHostedService).
/// </summary>
public class Neo4jInitializer : IHostedService
{
    private readonly IDriver _driver;
    private readonly IApplicationRepository _repo;
    private readonly ILogger<Neo4jInitializer> _log;

    public Neo4jInitializer(IDriver driver, IApplicationRepository repo, ILogger<Neo4jInitializer> log)
    {
        _driver = driver;
        _repo = repo;
        _log = log;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        // Neo4j peut démarrer après l'API : on retente la connexion quelques fois.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _driver.VerifyConnectivityAsync();
                break;
            }
            catch (Exception ex) when (attempt < 30 && !ct.IsCancellationRequested)
            {
                _log.LogWarning("Neo4j indisponible (tentative {Attempt}) : {Msg}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }

        await using (var session = _driver.AsyncSession())
        {
            await session.RunAsync(
                "CREATE CONSTRAINT app_id IF NOT EXISTS FOR (a:Application) REQUIRE a.id IS UNIQUE");
        }

        var existing = await _repo.GetAllAsync(ct);
        if (existing.Count == 0)
        {
            await SeedAsync(ct);
            _log.LogInformation("Graphe de démonstration initialisé.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private async Task SeedAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        Application New(string name, string owner, string stack, Criticality crit, DateTimeOffset? deployed) => new()
        {
            Name = name,
            Owner = owner,
            Stack = stack,
            Criticality = crit,
            LastDeployedAt = deployed,
            CreatedAt = now,
            UpdatedAt = now
        };

        var rh = await _repo.CreateAsync(New("Portail RH", "Équipe SIRH", "ASP.NET Core", Criticality.High,
            new DateTimeOffset(2026, 8, 20, 9, 30, 0, TimeSpan.Zero)), ct);
        var fournisseurs = await _repo.CreateAsync(New("Référentiel Fournisseurs", "Équipe Achats", "ASP.NET Core, PostgreSQL", Criticality.Medium,
            new DateTimeOffset(2026, 7, 3, 14, 0, 0, TimeSpan.Zero)), ct);
        var paiement = await _repo.CreateAsync(New("Passerelle Paiement", "Équipe Finance", "ASP.NET Core, Redis", Criticality.Vital,
            new DateTimeOffset(2026, 8, 28, 6, 15, 0, TimeSpan.Zero)), ct);

        // Portail RH et Référentiel Fournisseurs dépendent de la Passerelle Paiement.
        // -> l'« impact » de la Passerelle Paiement = ces deux applications.
        await _repo.AddDependencyAsync(rh.Id, paiement.Id, ct);
        await _repo.AddDependencyAsync(fournisseurs.Id, paiement.Id, ct);
    }
}
