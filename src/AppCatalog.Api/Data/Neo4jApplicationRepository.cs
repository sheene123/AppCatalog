using AppCatalog.Api.Domain;
using Neo4j.Driver;

namespace AppCatalog.Api.Data;

/// <summary>
/// Implémentation Neo4j du référentiel, en Cypher. Le driver (IDriver) est un
/// singleton thread-safe ; on ouvre une session courte par opération et on utilise
/// des fonctions de transaction (ExecuteRead/WriteAsync), la bonne pratique du driver.
/// </summary>
public class Neo4jApplicationRepository : IApplicationRepository
{
    private readonly IDriver _driver;

    public Neo4jApplicationRepository(IDriver driver) => _driver = driver;

    private const string Fields =
        "a.id AS id, a.name AS name, a.owner AS owner, a.stack AS stack, " +
        "a.criticality AS criticality, a.lastDeployedAt AS lastDeployedAt, " +
        "a.createdAt AS createdAt, a.updatedAt AS updatedAt";

    public async Task<IReadOnlyList<Application>> GetAllAsync(CancellationToken ct = default)
    {
        await using var session = _driver.AsyncSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync($"MATCH (a:Application) RETURN {Fields} ORDER BY a.name");
            var records = await cursor.ToListAsync();
            return (IReadOnlyList<Application>)records.Select(Map).ToList();
        });
    }

    public async Task<Application?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        await using var session = _driver.AsyncSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $"MATCH (a:Application {{id: $id}}) RETURN {Fields}", new { id });
            var records = await cursor.ToListAsync();
            return records.Count == 0 ? null : Map(records[0]);
        });
    }

    public async Task<Application> CreateAsync(Application app, CancellationToken ct = default)
    {
        await using var session = _driver.AsyncSession();
        return await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $@"CREATE (a:Application {{
                       id: $id, name: $name, owner: $owner, stack: $stack,
                       criticality: $criticality, lastDeployedAt: $lastDeployedAt,
                       createdAt: $createdAt, updatedAt: $updatedAt
                   }}) RETURN {Fields}",
                ToParams(app));
            var records = await cursor.ToListAsync();
            return Map(records[0]);
        });
    }

    public async Task<Application?> UpdateAsync(Application app, CancellationToken ct = default)
    {
        await using var session = _driver.AsyncSession();
        return await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $@"MATCH (a:Application {{id: $id}})
                   SET a.name = $name, a.owner = $owner, a.stack = $stack,
                       a.criticality = $criticality, a.lastDeployedAt = $lastDeployedAt,
                       a.updatedAt = $updatedAt
                   RETURN {Fields}",
                ToParams(app));
            var records = await cursor.ToListAsync();
            return records.Count == 0 ? null : Map(records[0]);
        });
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        await using var session = _driver.AsyncSession();
        return await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (a:Application {id: $id}) DETACH DELETE a RETURN count(a) AS c",
                new { id });
            var record = await cursor.SingleAsync();
            return record["c"].As<int>() > 0;
        });
    }

    public async Task<bool> AddDependencyAsync(string fromId, string toId, CancellationToken ct = default)
    {
        await using var session = _driver.AsyncSession();
        return await session.ExecuteWriteAsync(async tx =>
        {
            // MERGE : idempotent (pas de doublon de relation). Ne crée le lien que si
            // les deux applications existent.
            var cursor = await tx.RunAsync(
                @"MATCH (from:Application {id: $fromId}), (to:Application {id: $toId})
                  MERGE (from)-[r:DEPENDS_ON]->(to)
                  RETURN count(r) AS c",
                new { fromId, toId });
            var record = await cursor.SingleAsync();
            return record["c"].As<int>() > 0;
        });
    }

    public async Task<IReadOnlyList<Application>> GetDependenciesAsync(string id, CancellationToken ct = default)
    {
        await using var session = _driver.AsyncSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $"MATCH (:Application {{id: $id}})-[:DEPENDS_ON]->(a:Application) RETURN {Fields} ORDER BY a.name",
                new { id });
            var records = await cursor.ToListAsync();
            return (IReadOnlyList<Application>)records.Select(Map).ToList();
        });
    }

    public async Task<IReadOnlyList<Application>> GetImpactAsync(string id, CancellationToken ct = default)
    {
        await using var session = _driver.AsyncSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            // Qui dépend de cette application, directement OU indirectement (transitif) :
            // c'est là que le graphe brille (impossible aussi simplement en SQL).
            var cursor = await tx.RunAsync(
                $"MATCH (a:Application {{id: $id}})<-[:DEPENDS_ON*1..]-(dep:Application) " +
                $"WITH DISTINCT dep AS a RETURN {Fields} ORDER BY a.name",
                new { id });
            var records = await cursor.ToListAsync();
            return (IReadOnlyList<Application>)records.Select(Map).ToList();
        });
    }

    private static object ToParams(Application a) => new
    {
        id = a.Id,
        name = a.Name,
        owner = a.Owner,
        stack = a.Stack,
        criticality = a.Criticality.ToString(),
        lastDeployedAt = a.LastDeployedAt?.ToString("o"),
        createdAt = a.CreatedAt.ToString("o"),
        updatedAt = a.UpdatedAt.ToString("o")
    };

    private static Application Map(IRecord r) => new()
    {
        Id = r["id"].As<string>(),
        Name = r["name"].As<string>(),
        Owner = r["owner"].As<string>(),
        Stack = r["stack"].As<string?>() ?? string.Empty,
        Criticality = Enum.Parse<Criticality>(r["criticality"].As<string>()),
        LastDeployedAt = ParseDate(r["lastDeployedAt"]),
        CreatedAt = ParseDate(r["createdAt"]) ?? default,
        UpdatedAt = ParseDate(r["updatedAt"]) ?? default
    };

    private static DateTimeOffset? ParseDate(object value)
        => value is string s && !string.IsNullOrEmpty(s)
            ? DateTimeOffset.Parse(s, null, System.Globalization.DateTimeStyles.RoundtripKind)
            : null;
}
