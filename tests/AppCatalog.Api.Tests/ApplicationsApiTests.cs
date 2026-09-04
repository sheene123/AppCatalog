using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppCatalog.Api.Contracts;
using AppCatalog.Api.Domain;

namespace AppCatalog.Api.Tests;

/// <summary>
/// Tests d'intégration : ils tapent sur l'API HTTP réelle (via HttpClient),
/// traversent le controller, EF Core et la base in-memory. C'est le contrat de
/// bout en bout qui est vérifié, pas une méthode isolée.
/// </summary>
public class ApplicationsApiTests
{
    // Mêmes options que l'API : enums en texte. Nécessaire pour (dé)sérialiser
    // « Vital » côté client comme le fait le serveur.
    private static readonly JsonSerializerOptions Json = CreateJson();

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    [Fact]
    public async Task GetAll_retourne_les_donnees_de_seed()
    {
        using var factory = new AppCatalogFactory();
        var client = factory.CreateClient();

        var apps = await client.GetFromJsonAsync<List<ApplicationResponse>>(
            "/api/applications", Json);

        Assert.NotNull(apps);
        Assert.Equal(3, apps!.Count); // 3 applications insérées au seed
    }

    [Fact]
    public async Task GetById_inconnu_retourne_404()
    {
        using var factory = new AppCatalogFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/applications/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_valide_retourne_201_et_relit_l_application()
    {
        using var factory = new AppCatalogFactory();
        var client = factory.CreateClient();

        var request = new CreateApplicationRequest
        {
            Name = "Nouvelle App",
            Owner = "Équipe Test",
            Stack = "ASP.NET Core",
            Criticality = Criticality.High
        };

        var created = await client.PostAsJsonAsync("/api/applications", request, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var body = await created.Content.ReadFromJsonAsync<ApplicationResponse>(Json);
        Assert.NotNull(body);
        Assert.True(body!.Id > 0);
        Assert.Equal("Nouvelle App", body.Name);
        Assert.Equal(Criticality.High, body.Criticality);

        // L'en-tête Location doit permettre de relire la ressource.
        var reread = await client.GetFromJsonAsync<ApplicationResponse>(
            created.Headers.Location, Json);
        Assert.Equal(body.Id, reread!.Id);
    }

    [Fact]
    public async Task Create_invalide_retourne_400()
    {
        using var factory = new AppCatalogFactory();
        var client = factory.CreateClient();

        // Name vide -> viole [Required], la validation [ApiController] doit rejeter.
        var request = new CreateApplicationRequest { Name = "", Owner = "Équipe" };

        var response = await client.PostAsJsonAsync("/api/applications", request, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_modifie_l_application_existante()
    {
        using var factory = new AppCatalogFactory();
        var client = factory.CreateClient();

        var request = new UpdateApplicationRequest
        {
            Name = "Portail RH v2",
            Owner = "Équipe SIRH",
            Stack = "ASP.NET Core, SQL Server",
            Criticality = Criticality.Vital
        };

        var response = await client.PutAsJsonAsync("/api/applications/1", request, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<ApplicationResponse>(Json);
        Assert.Equal("Portail RH v2", updated!.Name);
        Assert.Equal(Criticality.Vital, updated.Criticality);
    }

    [Fact]
    public async Task Delete_puis_relecture_retourne_404()
    {
        using var factory = new AppCatalogFactory();
        var client = factory.CreateClient();

        var deleted = await client.DeleteAsync("/api/applications/2");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var reread = await client.GetAsync("/api/applications/2");
        Assert.Equal(HttpStatusCode.NotFound, reread.StatusCode);
    }

    [Fact]
    public async Task Health_repond_healthy()
    {
        using var factory = new AppCatalogFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }
}
