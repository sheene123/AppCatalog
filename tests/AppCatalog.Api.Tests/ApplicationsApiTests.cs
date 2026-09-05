using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppCatalog.Api.Contracts;
using AppCatalog.Api.Domain;

namespace AppCatalog.Api.Tests;

/// <summary>
/// Tests d'intégration contre un vrai Neo4j (Testcontainers) : ils traversent
/// l'API HTTP, le controller, le repository Cypher et la base graphe.
/// Chaque test crée puis nettoie ses propres données pour rester indépendant.
/// </summary>
public class ApplicationsApiTests : IClassFixture<AppCatalogFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = CreateJson();

    public ApplicationsApiTests(AppCatalogFactory factory) => _client = factory.CreateClient();

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private async Task<ApplicationResponse> CreateAsync(string name, Criticality crit = Criticality.Medium)
    {
        var req = new CreateApplicationRequest { Name = name, Owner = "Tests", Stack = "X", Criticality = crit };
        var res = await _client.PostAsJsonAsync("/api/applications", req, Json);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ApplicationResponse>(Json))!;
    }

    [Fact]
    public async Task Health_repond_healthy()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetAll_contient_les_donnees_de_seed()
    {
        var apps = await _client.GetFromJsonAsync<List<ApplicationResponse>>("/api/applications", Json);
        Assert.NotNull(apps);
        Assert.True(apps!.Count >= 3);
        Assert.Contains(apps, a => a.Name == "Passerelle Paiement");
    }

    [Fact]
    public async Task GetById_inconnu_retourne_404()
    {
        var response = await _client.GetAsync("/api/applications/inconnu-xyz");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_valide_retourne_201_puis_relecture_ok()
    {
        var created = await CreateAsync("Créée par test", Criticality.High);
        Assert.False(string.IsNullOrEmpty(created.Id));
        Assert.Equal(Criticality.High, created.Criticality);

        var reread = await _client.GetFromJsonAsync<ApplicationResponse>(
            $"/api/applications/{created.Id}", Json);
        Assert.Equal(created.Id, reread!.Id);

        await _client.DeleteAsync($"/api/applications/{created.Id}"); // nettoyage
    }

    [Fact]
    public async Task Create_invalide_retourne_400()
    {
        var req = new CreateApplicationRequest { Name = "", Owner = "X" };
        var response = await _client.PostAsJsonAsync("/api/applications", req, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_modifie_l_application()
    {
        var created = await CreateAsync("À modifier");
        var req = new UpdateApplicationRequest
        {
            Name = "Modifiée",
            Owner = "Tests",
            Stack = "Y",
            Criticality = Criticality.Vital
        };
        var response = await _client.PutAsJsonAsync($"/api/applications/{created.Id}", req, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<ApplicationResponse>(Json);
        Assert.Equal("Modifiée", updated!.Name);
        Assert.Equal(Criticality.Vital, updated.Criticality);

        await _client.DeleteAsync($"/api/applications/{created.Id}");
    }

    [Fact]
    public async Task Delete_puis_relecture_retourne_404()
    {
        var created = await CreateAsync("À supprimer");
        var deleted = await _client.DeleteAsync($"/api/applications/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var reread = await _client.GetAsync($"/api/applications/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, reread.StatusCode);
    }

    [Fact]
    public async Task Graphe_dependance_et_impact_transitif()
    {
        // A dépend de B, B dépend de C  =>  impact(C) contient A et B.
        var a = await CreateAsync("Graphe A");
        var b = await CreateAsync("Graphe B");
        var c = await CreateAsync("Graphe C");

        await LinkAsync(a.Id, b.Id);
        await LinkAsync(b.Id, c.Id);

        // Dépendances directes de A = { B }
        var depsA = await _client.GetFromJsonAsync<List<ApplicationResponse>>(
            $"/api/applications/{a.Id}/dependencies", Json);
        Assert.Contains(depsA!, x => x.Id == b.Id);

        // Impact de C (transitif) = { A, B }
        var impactC = await _client.GetFromJsonAsync<List<ApplicationResponse>>(
            $"/api/applications/{c.Id}/impact", Json);
        Assert.Contains(impactC!, x => x.Id == a.Id);
        Assert.Contains(impactC!, x => x.Id == b.Id);

        foreach (var id in new[] { a.Id, b.Id, c.Id })
            await _client.DeleteAsync($"/api/applications/{id}");
    }

    private async Task LinkAsync(string fromId, string toId)
    {
        var res = await _client.PostAsJsonAsync(
            $"/api/applications/{fromId}/dependencies",
            new AddDependencyRequest { TargetId = toId }, Json);
        res.EnsureSuccessStatusCode();
    }
}
