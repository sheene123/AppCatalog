using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppCatalog.Web.Api;

/// <summary>
/// Client typé vers l'API AppCatalog. Les écritures portent la clé d'administration
/// (si l'utilisateur est connecté) ; l'API la vérifie côté serveur.
/// </summary>
public class ApplicationsClient
{
    private readonly HttpClient _http;
    private readonly AdminSession _session;

    private static readonly JsonSerializerOptions Json = CreateJson();

    public ApplicationsClient(HttpClient http, AdminSession session)
    {
        _http = http;
        _session = session;
    }

    // --- Lecture (public) ---

    public async Task<IReadOnlyList<ApplicationModel>> GetAllAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<ApplicationModel>>("api/applications", Json, ct) ?? [];

    public async Task<IReadOnlyList<ApplicationModel>> GetDependenciesAsync(string id, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<ApplicationModel>>($"api/applications/{id}/dependencies", Json, ct) ?? [];

    public async Task<IReadOnlyList<ApplicationModel>> GetImpactAsync(string id, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<ApplicationModel>>($"api/applications/{id}/impact", Json, ct) ?? [];

    public async Task<GraphData> GetGraphAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<GraphData>("api/applications/graph", Json, ct) ?? new GraphData([], []);

    // --- Écriture (nécessite la clé d'administration) ---

    public async Task<ApplicationModel?> CreateAsync(CreateApplicationInput input, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "api/applications")
        {
            Content = JsonContent.Create(input, options: Json)
        };
        AddAuth(req);
        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApplicationModel>(Json, ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"api/applications/{id}");
        AddAuth(req);
        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddDependencyAsync(string fromId, string toId, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"api/applications/{fromId}/dependencies")
        {
            Content = JsonContent.Create(new { targetId = toId }, options: Json)
        };
        AddAuth(req);
        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Vérifie une clé d'administration auprès de l'API (page de connexion).</summary>
    public async Task<bool> VerifyKeyAsync(string key, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/auth/verify");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var response = await _http.SendAsync(req, ct);
        return response.IsSuccessStatusCode;
    }

    private void AddAuth(HttpRequestMessage req)
    {
        if (_session.Key is { } key)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
