using Microsoft.Extensions.Diagnostics.HealthChecks;
using Neo4j.Driver;

namespace AppCatalog.Api.Health;

/// <summary>Vérifie que Neo4j répond (RETURN 1). Exposé sur /health.</summary>
public class Neo4jHealthCheck : IHealthCheck
{
    private readonly IDriver _driver;

    public Neo4jHealthCheck(IDriver driver) => _driver = driver;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var session = _driver.AsyncSession();
            var cursor = await session.RunAsync("RETURN 1");
            await cursor.ConsumeAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Neo4j injoignable", ex);
        }
    }
}
