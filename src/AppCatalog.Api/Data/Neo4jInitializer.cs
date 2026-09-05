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
        var apps = new Dictionary<string, Application>();

        async Task<string> Add(string name, string owner, string stack, Criticality crit, int daysAgo)
        {
            var app = await _repo.CreateAsync(new Application
            {
                Name = name,
                Owner = owner,
                Stack = stack,
                Criticality = crit,
                LastDeployedAt = now.AddDays(-daysAgo),
                CreatedAt = now,
                UpdatedAt = now
            }, ct);
            apps[name] = app;
            return app.Id;
        }

        // Canaux (applications exposées aux usagers)
        await Add("Portail Voyageurs", "Équipe Digital", "Next.js, React", Criticality.High, 3);
        await Add("Application Mobile", "Équipe Mobile", "Kotlin, Swift", Criticality.High, 6);
        await Add("Bornes d'enregistrement", "Équipe Self-Service", "C#, WPF", Criticality.Vital, 12);
        await Add("Affichage des vols (FIDS)", "Équipe Exploitation", "Angular, Node.js", Criticality.Vital, 2);
        await Add("Back-office Agents", "Équipe Exploitation", "ASP.NET Core, Blazor", Criticality.Medium, 9);

        // Services applicatifs
        await Add("API Gateway", "Équipe Plateforme", "Kong, Node.js", Criticality.Vital, 5);
        await Add("Service Authentification", "Équipe Sécurité", "Keycloak, OAuth2", Criticality.Vital, 20);
        await Add("Service Réservation", "Équipe Réservation", "Java, Spring Boot", Criticality.High, 7);
        await Add("Service Paiement", "Équipe Finance", "ASP.NET Core", Criticality.Vital, 4);
        await Add("Service Bagages", "Équipe Bagages", "Go", Criticality.High, 15);
        await Add("Service Notifications", "Équipe Digital", "Python, FastAPI", Criticality.Low, 22);

        // Données et systèmes externes
        await Add("Référentiel des vols", "Équipe Données", "PostgreSQL", Criticality.Vital, 30);
        await Add("Base Passagers", "Équipe Données", "Oracle", Criticality.Vital, 45);
        await Add("Passerelle Bancaire", "Équipe Finance", "Partenaire externe", Criticality.Vital, 60);

        // Dépendances : (source) dépend de (cible).
        var links = new (string From, string To)[]
        {
            ("Portail Voyageurs", "API Gateway"),
            ("Portail Voyageurs", "Service Authentification"),
            ("Application Mobile", "API Gateway"),
            ("Application Mobile", "Service Authentification"),
            ("Bornes d'enregistrement", "API Gateway"),
            ("Bornes d'enregistrement", "Service Bagages"),
            ("Affichage des vols (FIDS)", "Référentiel des vols"),
            ("Back-office Agents", "API Gateway"),
            ("API Gateway", "Service Réservation"),
            ("API Gateway", "Service Paiement"),
            ("API Gateway", "Service Bagages"),
            ("API Gateway", "Service Notifications"),
            ("Service Réservation", "Référentiel des vols"),
            ("Service Réservation", "Base Passagers"),
            ("Service Paiement", "Passerelle Bancaire"),
            ("Service Paiement", "Base Passagers"),
            ("Service Bagages", "Référentiel des vols"),
            ("Service Authentification", "Base Passagers"),
        };
        foreach (var (from, to) in links)
            await _repo.AddDependencyAsync(apps[from].Id, apps[to].Id, ct);
    }
}
