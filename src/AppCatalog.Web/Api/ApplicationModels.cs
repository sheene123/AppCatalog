namespace AppCatalog.Web.Api;

// Modèles côté frontend. Volontairement définis ici plutôt que réutilisés depuis
// le projet API : les deux services sont découplés et ne communiquent que par
// HTTP/JSON, pas par référence de code. C'est le principe d'une archi microservices.

public enum Criticality
{
    Low,
    Medium,
    High,
    Vital
}

public record ApplicationModel(
    string Id,
    string Name,
    string Owner,
    string Stack,
    Criticality Criticality,
    DateTimeOffset? LastDeployedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// Formulaire de création (lié au champ du même nom côté API).
public class CreateApplicationInput
{
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Stack { get; set; } = string.Empty;
    public Criticality Criticality { get; set; } = Criticality.Medium;
}
