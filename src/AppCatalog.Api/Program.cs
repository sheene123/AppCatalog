using System.Text.Json.Serialization;
using AppCatalog.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Services (injection de dépendances) ---

builder.Services.AddControllers()
    // Sérialise les enums en texte (« Vital ») plutôt qu'en entier dans le JSON :
    // contrat plus lisible et stable, aligné sur le stockage en base.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// EF Core sur SQLite. La chaîne de connexion vient de appsettings.json,
// surchargeable par variable d'environnement (ConnectionStrings__AppCatalog).
builder.Services.AddDbContext<AppCatalogDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("AppCatalog")
        ?? "Data Source=appcatalog.db"));

// Réponses d'erreur normalisées au format RFC 7807 (application/problem+json).
builder.Services.AddProblemDetails();

// Sonde de disponibilité, utilisée par Docker et l'orchestrateur.
builder.Services.AddHealthChecks();

// Swagger / OpenAPI.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1",
    new() { Title = "AppCatalog API", Version = "v1" }));

var app = builder.Build();

// --- Pipeline HTTP ---

// Traduit les exceptions non gérées en ProblemDetails plutôt qu'en page d'erreur brute.
app.UseExceptionHandler();
app.UseStatusCodePages();

// Swagger exposé dans tous les environnements : c'est un outil interne de la DSI,
// pas une API publique. En contexte grand public on le limiterait au développement.
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapHealthChecks("/health");

// Applique les migrations au démarrage (hors tests, qui montent leur propre base).
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppCatalogDbContext>();
    db.Database.Migrate();
}

app.Run();

// Rendu public pour que le projet de tests puisse instancier l'application
// via WebApplicationFactory<Program>. Les instructions « top-level » génèrent
// sinon une classe Program interne, inaccessible depuis les tests.
public partial class Program { }
