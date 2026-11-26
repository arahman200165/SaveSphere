using SaveSphere.Infrastructure.Endpoints;
using SaveSphere.SharedKernel;

namespace SaveSphere.Api.Health;

public sealed record HealthResponse(string Status, IReadOnlyDictionary<string, string> Checks);

/// <summary>
/// Liveness (<c>/api/v1/health/live</c>, process only) and readiness (<c>/api/v1/health</c>, database reachable and
/// migrations current). Responses expose only coarse states: never connection details, versions or exception text.
/// </summary>
internal sealed partial class HealthEndpoints(ILogger<HealthEndpoints> logger) : IEndpointModule
{
    private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromSeconds(5);

    public void MapEndpoints(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var group = routes.MapGroup("/health").WithTags("Health");

        group.MapGet("/live", () => TypedResults.Ok(new HealthResponse("healthy", new Dictionary<string, string>())))
            .WithName("GetLiveness")
            .Produces<HealthResponse>(StatusCodes.Status200OK);

        group.MapGet(string.Empty, GetReadinessAsync)
            .WithName("GetReadiness")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);
    }

    private async Task<IResult> GetReadinessAsync(IEnumerable<IModuleDatabase> modules, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReadinessTimeout);

        var checks = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            var databaseKey = $"{module.ModuleName}.database";
            var migrationsKey = $"{module.ModuleName}.migrations";
            try
            {
                if (!await module.CanConnectAsync(timeout.Token))
                {
                    checks[databaseKey] = "down";
                    checks[migrationsKey] = "unknown";
                    continue;
                }

                checks[databaseKey] = "up";
                var pending = await module.GetPendingMigrationsAsync(timeout.Token);
                checks[migrationsKey] = pending.Count == 0 ? "current" : "pending";
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                LogReadinessFailure(logger, module.ModuleName, ex);
                checks[databaseKey] = "down";
                checks[migrationsKey] = "unknown";
            }
        }

        var healthy = checks.Values.All(v => v is "up" or "current");
        var body = new HealthResponse(healthy ? "healthy" : "unhealthy", checks);
        return Results.Json(body, statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Readiness check failed for module {Module}")]
    private static partial void LogReadinessFailure(ILogger logger, string module, Exception exception);
}
