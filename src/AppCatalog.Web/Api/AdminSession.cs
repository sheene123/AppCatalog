namespace AppCatalog.Web.Api;

/// <summary>
/// État d'authentification admin, porté par le circuit Blazor (scoped).
/// La consultation est publique ; seule l'administration (ajout/suppression)
/// requiert d'être connecté. La clé n'est jamais stockée durablement.
/// </summary>
public class AdminSession
{
    public string? Key { get; private set; }
    public bool IsAdmin => !string.IsNullOrEmpty(Key);

    public void SignIn(string key) => Key = key;
    public void SignOut() => Key = null;
}
