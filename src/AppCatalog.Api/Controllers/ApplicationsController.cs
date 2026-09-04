using AppCatalog.Api.Contracts;
using AppCatalog.Api.Data;
using AppCatalog.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppCatalog.Api.Controllers;

/// <summary>
/// Points d'entrée REST du référentiel d'applications.
/// [ApiController] active les conventions Web API : validation automatique du modèle
/// (400 + ProblemDetails si invalide), binding des paramètres, etc.
/// </summary>
[ApiController]
[Route("api/[controller]")] // -> /api/applications
public class ApplicationsController : ControllerBase
{
    private readonly AppCatalogDbContext _db;

    // Le DbContext est injecté par le conteneur de dépendances (voir Program.cs).
    public ApplicationsController(AppCatalogDbContext db) => _db = db;

    /// <summary>Liste toutes les applications.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ApplicationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ApplicationResponse>>> GetAll(CancellationToken ct)
    {
        // AsNoTracking : lecture seule, EF ne suit pas les entités -> plus rapide.
        var items = await _db.Applications
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => a.ToResponse())
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>Récupère une application par son identifiant.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationResponse>> GetById(int id, CancellationToken ct)
    {
        var app = await _db.Applications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        return app is null ? NotFound() : Ok(app.ToResponse());
    }

    /// <summary>Crée une nouvelle application.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApplicationResponse>> Create(
        CreateApplicationRequest request, CancellationToken ct)
    {
        var app = request.ToEntity();
        var now = DateTimeOffset.UtcNow;
        app.CreatedAt = now;
        app.UpdatedAt = now;

        _db.Applications.Add(app);
        await _db.SaveChangesAsync(ct);

        // 201 Created + en-tête Location pointant vers GET /api/applications/{id}.
        return CreatedAtAction(nameof(GetById), new { id = app.Id }, app.ToResponse());
    }

    /// <summary>Met à jour une application existante (remplacement complet).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationResponse>> Update(
        int id, UpdateApplicationRequest request, CancellationToken ct)
    {
        var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (app is null)
            return NotFound();

        app.Name = request.Name;
        app.Owner = request.Owner;
        app.Stack = request.Stack;
        app.Criticality = request.Criticality;
        app.LastDeployedAt = request.LastDeployedAt;
        app.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(app.ToResponse());
    }

    /// <summary>Supprime une application.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var app = await _db.Applications.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (app is null)
            return NotFound();

        _db.Applications.Remove(app);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
