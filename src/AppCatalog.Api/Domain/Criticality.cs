namespace AppCatalog.Api.Domain;

/// <summary>
/// Niveau de criticité métier d'une application du SI.
/// Stocké en base sous forme de texte pour rester lisible et robuste à
/// l'ajout de nouvelles valeurs.
/// </summary>
public enum Criticality
{
    Low,
    Medium,
    High,
    Vital
}
