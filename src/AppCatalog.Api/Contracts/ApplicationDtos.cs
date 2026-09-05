using System.ComponentModel.DataAnnotations;
using AppCatalog.Api.Domain;

namespace AppCatalog.Api.Contracts;

/// <summary>
/// Contrats (DTO) échangés avec les clients. Séparés de l'entité pour ne pas
/// exposer les champs d'audit en écriture et découpler le contrat du stockage.
/// </summary>

public record ApplicationResponse(
    string Id,
    string Name,
    string Owner,
    string Stack,
    Criticality Criticality,
    DateTimeOffset? LastDeployedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record CreateApplicationRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 1)]
    public string Owner { get; init; } = string.Empty;

    [StringLength(400)]
    public string Stack { get; init; } = string.Empty;

    [EnumDataType(typeof(Criticality))]
    public Criticality Criticality { get; init; } = Criticality.Medium;

    public DateTimeOffset? LastDeployedAt { get; init; }
}

public record UpdateApplicationRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 1)]
    public string Owner { get; init; } = string.Empty;

    [StringLength(400)]
    public string Stack { get; init; } = string.Empty;

    [EnumDataType(typeof(Criticality))]
    public Criticality Criticality { get; init; } = Criticality.Medium;

    public DateTimeOffset? LastDeployedAt { get; init; }
}

/// <summary>Corps de la requête pour lier une dépendance : « cette appli dépend de TargetId ».</summary>
public record AddDependencyRequest
{
    [Required]
    public string TargetId { get; init; } = string.Empty;
}

/// <summary>Une relation de dépendance du graphe (From dépend de To).</summary>
public record GraphEdge(string From, string To);

/// <summary>Le graphe complet : nœuds (applications) et arêtes (dépendances).</summary>
public record GraphResponse(IEnumerable<ApplicationResponse> Nodes, IEnumerable<GraphEdge> Edges);

public static class ApplicationMapping
{
    public static ApplicationResponse ToResponse(this Application a) => new(
        a.Id, a.Name, a.Owner, a.Stack, a.Criticality,
        a.LastDeployedAt, a.CreatedAt, a.UpdatedAt);

    public static Application ToEntity(this CreateApplicationRequest r) => new()
    {
        Name = r.Name,
        Owner = r.Owner,
        Stack = r.Stack,
        Criticality = r.Criticality,
        LastDeployedAt = r.LastDeployedAt
    };
}
