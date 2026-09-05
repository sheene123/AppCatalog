using AppCatalog.Api.Contracts;
using AppCatalog.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace AppCatalog.Api.Controllers;

/// <summary>
/// Points d'entrée REST du référentiel. Le controller ne connaît que
/// IApplicationRepository : il ignore que le stockage est un graphe Neo4j.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationRepository _repo;

    public ApplicationsController(IApplicationRepository repo) => _repo = repo;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ApplicationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ApplicationResponse>>> GetAll(CancellationToken ct)
    {
        var items = await _repo.GetAllAsync(ct);
        return Ok(items.Select(a => a.ToResponse()));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationResponse>> GetById(string id, CancellationToken ct)
    {
        var app = await _repo.GetByIdAsync(id, ct);
        return app is null ? NotFound() : Ok(app.ToResponse());
    }

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

        var created = await _repo.CreateAsync(app, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToResponse());
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationResponse>> Update(
        string id, UpdateApplicationRequest request, CancellationToken ct)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing is null)
            return NotFound();

        existing.Name = request.Name;
        existing.Owner = request.Owner;
        existing.Stack = request.Stack;
        existing.Criticality = request.Criticality;
        existing.LastDeployedAt = request.LastDeployedAt;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await _repo.UpdateAsync(existing, ct);
        return updated is null ? NotFound() : Ok(updated.ToResponse());
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var deleted = await _repo.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }

    // --- Partie graphe : dépendances entre applications ---

    /// <summary>Le graphe complet (nœuds + dépendances), pour la cartographie.</summary>
    [HttpGet("graph")]
    [ProducesResponseType(typeof(GraphResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GraphResponse>> GetGraph(CancellationToken ct)
    {
        var (nodes, edges) = await _repo.GetGraphAsync(ct);
        return Ok(new GraphResponse(
            nodes.Select(a => a.ToResponse()),
            edges.Select(e => new GraphEdge(e.From, e.To))));
    }

    /// <summary>Déclare que l'application {id} dépend d'une autre (TargetId).</summary>
    [HttpPost("{id}/dependencies")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddDependency(
        string id, AddDependencyRequest request, CancellationToken ct)
    {
        var ok = await _repo.AddDependencyAsync(id, request.TargetId, ct);
        return ok ? NoContent() : NotFound(); // 404 si l'une des deux applis n'existe pas
    }

    /// <summary>Applications dont {id} dépend (voisins directs sortants).</summary>
    [HttpGet("{id}/dependencies")]
    [ProducesResponseType(typeof(IEnumerable<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ApplicationResponse>>> GetDependencies(
        string id, CancellationToken ct)
    {
        if (await _repo.GetByIdAsync(id, ct) is null)
            return NotFound();
        var deps = await _repo.GetDependenciesAsync(id, ct);
        return Ok(deps.Select(a => a.ToResponse()));
    }

    /// <summary>Applications impactées si {id} tombe (qui en dépendent, transitivement).</summary>
    [HttpGet("{id}/impact")]
    [ProducesResponseType(typeof(IEnumerable<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ApplicationResponse>>> GetImpact(
        string id, CancellationToken ct)
    {
        if (await _repo.GetByIdAsync(id, ct) is null)
            return NotFound();
        var impacted = await _repo.GetImpactAsync(id, ct);
        return Ok(impacted.Select(a => a.ToResponse()));
    }
}
