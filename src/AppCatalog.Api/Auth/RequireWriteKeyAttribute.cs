using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AppCatalog.Api.Auth;

/// <summary>
/// Exige une clé d'écriture sur l'action (en-tête « Authorization: Bearer &lt;clé&gt; »).
/// La clé attendue vient de la configuration (Api:WriteKey), idéalement d'un secret.
/// Si aucune clé n'est configurée (dev / tests), l'écriture reste ouverte.
/// La lecture n'est jamais protégée : le catalogue est public.
/// </summary>
public sealed class RequireWriteKeyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expected = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["Api:WriteKey"];

        if (string.IsNullOrEmpty(expected))
            return; // pas de clé configurée -> écriture ouverte (dev/test)

        const string prefix = "Bearer ";
        var header = context.HttpContext.Request.Headers.Authorization.ToString();
        var provided = header.StartsWith(prefix, StringComparison.Ordinal)
            ? header[prefix.Length..]
            : null;

        // Comparaison à temps constant (anti timing-attack).
        if (provided is null || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected)))
        {
            context.Result = new UnauthorizedResult(); // 401
        }
    }
}
