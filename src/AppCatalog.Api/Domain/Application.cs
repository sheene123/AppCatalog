namespace AppCatalog.Api.Domain;

/// <summary>
/// Une application recensée dans le référentiel du SI.
/// C'est l'entité persistée : elle ne sort jamais telle quelle de l'API,
/// on passe par des DTO (voir Contracts/) pour découpler le modèle de stockage
/// du contrat exposé aux clients.
/// </summary>
public class Application
{
    public int Id { get; set; }

    /// <summary>Nom de l'application (ex. « Portail RH »).</summary>
    public required string Name { get; set; }

    /// <summary>Équipe ou personne responsable de l'application.</summary>
    public required string Owner { get; set; }

    /// <summary>Pile technique, en texte libre (ex. « ASP.NET Core, SQL Server »).</summary>
    public string Stack { get; set; } = string.Empty;

    /// <summary>Criticité métier, utilisée pour prioriser exploitation et sécurité.</summary>
    public Criticality Criticality { get; set; } = Criticality.Medium;

    /// <summary>Date du dernier déploiement en production. Null si jamais déployée.</summary>
    public DateTimeOffset? LastDeployedAt { get; set; }

    // Champs d'audit : renseignés par l'API, jamais par le client.
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
