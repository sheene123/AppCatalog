using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppCatalog.Web.Api;

/// <summary>
/// Client typé qui parle à l'API AppCatalog en HTTP/JSON.
/// L'adresse de base est injectée (voir Program.cs) : en local elle pointe vers
/// http://localhost:5080, dans Kubernetes vers http://appcatalog-api (nom de
/// service interne résolu par le DNS du cluster).
/// </summary>
public class ApplicationsClient
{
    private readonly HttpClient _http;

    // Mêmes options que l'API : enums en texte (« Vital »).
    private static readonly JsonSerializerOptions Json = CreateJson();

    public ApplicationsClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<ApplicationModel>> GetAllAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<ApplicationModel>>("api/applications", Json, ct) ?? [];

    public async Task<ApplicationModel?> CreateAsync(CreateApplicationInput input, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/applications", input, Json, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApplicationModel>(Json, ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/applications/{id}", ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Applications dont {id} dépend (voisins sortants directs).</summary>
    public async Task<IReadOnlyList<ApplicationModel>> GetDependenciesAsync(string id, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<ApplicationModel>>($"api/applications/{id}/dependencies", Json, ct) ?? [];

    /// <summary>Applications impactées si {id} tombe (qui en dépendent, transitivement).</summary>
    public async Task<IReadOnlyList<ApplicationModel>> GetImpactAsync(string id, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<ApplicationModel>>($"api/applications/{id}/impact", Json, ct) ?? [];

    /// <summary>Déclare que {fromId} dépend de {toId}.</summary>
    public async Task AddDependencyAsync(string fromId, string toId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/applications/{fromId}/dependencies", new { targetId = toId }, Json, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Le graphe complet (nœuds + dépendances) pour la cartographie.</summary>
    public async Task<GraphData> GetGraphAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<GraphData>("api/applications/graph", Json, ct)
           ?? new GraphData([], []);

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
