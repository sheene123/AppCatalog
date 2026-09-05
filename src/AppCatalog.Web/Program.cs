using AppCatalog.Web.Api;
using AppCatalog.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Adresse de l'API : configurable (clé ApiBaseUrl / variable d'env ApiBaseUrl).
// En local : http://localhost:5080. Dans Kubernetes : http://appcatalog-api.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5080";

// Client typé enregistré dans le conteneur DI. On force un « / » final pour que
// les chemins relatifs (« api/applications ») se combinent correctement.
builder.Services.AddHttpClient<ApplicationsClient>(client =>
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/"));

// État d'authentification admin, porté par le circuit Blazor.
builder.Services.AddScoped<AdminSession>();

// Sonde de disponibilité (utilisée par Kubernetes).
builder.Services.AddHealthChecks();

var app = builder.Build();

// En-têtes de sécurité sur toutes les réponses.
// CSP adaptée à Blazor Server (scripts locaux + WebSocket SignalR même origine).
app.Use(async (context, next) =>
{
    var h = context.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";                 // pas de MIME sniffing
    h["X-Frame-Options"] = "DENY";                           // anti-clickjacking
    h["Referrer-Policy"] = "no-referrer";
    h["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains"; // HSTS (actif derrière TLS)
    h["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "connect-src 'self' ws: wss:; " +
        "img-src 'self' data:; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHealthChecks("/health");

app.Run();
