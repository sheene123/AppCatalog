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

// Sonde de disponibilité (utilisée par Kubernetes).
builder.Services.AddHealthChecks();

var app = builder.Build();

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
