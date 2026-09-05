using AppCatalog.Api.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AppCatalog.Api.Controllers;

/// <summary>Vérification de la clé d'écriture (utilisée par la page de connexion).</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    /// <summary>Renvoie 200 si la clé fournie est valide, 401 sinon.</summary>
    [HttpGet("verify")]
    [RequireWriteKey]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Verify() => Ok(new { authenticated = true });
}
