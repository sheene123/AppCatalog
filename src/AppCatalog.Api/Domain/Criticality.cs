namespace AppCatalog.Api.Domain;

/// <summary>
/// Niveau de criticité métier d'une application du SI.
/// Stocké en base sous forme de texte (voir AppCatalogDbContext) pour rester
/// lisible dans la base et robuste à l'ajout de nouvelles valeurs.
/// </summary>
public enum Criticality
{
    Low,
    Medium,
    High,
    Vital
}
