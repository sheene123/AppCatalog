namespace AppCatalog.Api.Domain;

/// <summary>
/// Une application recensée dans le référentiel du SI.
/// Persistée comme un nœud (:Application) dans Neo4j. Les dépendances entre
/// applications sont des relations (:Application)-[:DEPENDS_ON]->(:Application),
/// pas des champs de ce type.
/// </summary>
public class Application
{
    /// <summary>Identifiant unique (GUID). Clé du nœud dans le graphe.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    public required string Name { get; set; }
    public required string Owner { get; set; }
    public string Stack { get; set; } = string.Empty;
    public Criticality Criticality { get; set; } = Criticality.Medium;
    public DateTimeOffset? LastDeployedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
