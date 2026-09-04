using System.ComponentModel.DataAnnotations;
using AppCatalog.Api.Domain;

namespace AppCatalog.Api.Contracts;

/// <summary>
/// Contrats (DTO) échangés avec les clients de l'API.
/// On les sépare de l'entité Application pour deux raisons :
///  - ne pas exposer les champs d'audit en écriture (CreatedAt/UpdatedAt) ;
///  - pouvoir faire évoluer le stockage sans casser le contrat public.
/// Ce sont des « records » : types immuables, égalité par valeur, parfaits pour du transport.
/// </summary>

public record ApplicationResponse(
    int Id,
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

/// <summary>
/// Conversions entité &lt;-&gt; DTO. Fait à la main volontairement (pas d'AutoMapper) :
/// le mapping reste explicite et lisible, ce qui compte plus que la concision ici.
/// </summary>
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
