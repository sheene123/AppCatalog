using AppCatalog.Api.Domain;

namespace AppCatalog.Api.Data;

/// <summary>
/// Accès au référentiel. Abstrait derrière une interface pour découpler le
/// controller du détail Neo4j (et permettre des tests avec un faux repository).
/// </summary>
public interface IApplicationRepository
{
    Task<IReadOnlyList<Application>> GetAllAsync(CancellationToken ct = default);
    Task<Application?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Application> CreateAsync(Application app, CancellationToken ct = default);
    Task<Application?> UpdateAsync(Application app, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);

    // Partie « graphe » : dépendances entre applications.
    Task<bool> AddDependencyAsync(string fromId, string toId, CancellationToken ct = default);
    Task<IReadOnlyList<Application>> GetDependenciesAsync(string id, CancellationToken ct = default);

    /// <summary>Applications impactées si celle-ci tombe (qui dépendent d'elle, transitivement).</summary>
    Task<IReadOnlyList<Application>> GetImpactAsync(string id, CancellationToken ct = default);

    /// <summary>Le graphe complet : tous les nœuds et toutes les relations DEPENDS_ON.</summary>
    Task<(IReadOnlyList<Application> Nodes, IReadOnlyList<(string From, string To)> Edges)> GetGraphAsync(
        CancellationToken ct = default);
}
