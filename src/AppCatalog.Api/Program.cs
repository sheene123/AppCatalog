using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AppCatalog.Api.Data;
using AppCatalog.Api.Health;
using Neo4j.Driver;

var builder = WebApplication.CreateBuilder(args);

// --- Services (injection de dépendances) ---

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Limitation de débit : protège l'API contre les abus / pics de charge.
// Fenêtre fixe par IP, paramétrable (RateLimiting:PermitLimit / :WindowSeconds).
// Au-delà du quota -> 429 Too Many Requests. /health est exempté (sondes k8s).
var permitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PermitLimit") ?? 100;
var windowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:WindowSeconds") ?? 10;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (context.Request.Path.StartsWithSegments("/health"))
            return RateLimitPartition.GetNoLimiter("health");

        var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0
        });
    });
});

// Le driver Neo4j est thread-safe : un seul pour toute l'application (singleton).
// On lit les paramètres via le ServiceProvider (config finale) plutôt qu'au moment
// du builder : ainsi tout override de configuration (tests d'intégration) est pris
// en compte. Paramètres : section « Neo4j » ou variables d'env Neo4j__Uri/User/Password.
// Sans mot de passe (tests, Neo4j local sans auth) on se connecte en AuthTokens.None.
builder.Services.AddSingleton<IDriver>(sp =>
{
    var settings = sp.GetRequiredService<IConfiguration>()
        .GetSection("Neo4j").Get<Neo4jSettings>() ?? new Neo4jSettings();
    var authToken = string.IsNullOrEmpty(settings.Password)
        ? AuthTokens.None
        : AuthTokens.Basic(settings.User, settings.Password);
    return GraphDatabase.Driver(settings.Uri, authToken);
});

builder.Services.AddSingleton<IApplicationRepository, Neo4jApplicationRepository>();
builder.Services.AddHostedService<Neo4jInitializer>();

builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddCheck<Neo4jHealthCheck>("neo4j");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1",
    new() { Title = "AppCatalog API", Version = "v1" }));

var app = builder.Build();

// --- Pipeline HTTP ---

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Rendu public pour les tests d'intégration (WebApplicationFactory<Program>).
public partial class Program { }
