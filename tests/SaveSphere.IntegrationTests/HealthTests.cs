using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SaveSphere.Api.Health;
using SaveSphere.TestSupport;

namespace SaveSphere.IntegrationTests;

[Requirement("NFR-01")]
[Requirement("NFR-07")]
public sealed class HealthTests(PostgresFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Ready_when_the_database_is_reachable_and_migrations_are_current()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: true);
        await using var factory = new TestApiFactory(connection);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("healthy", body.Status);
        Assert.Equal("up", body.Checks["core.database"]);
        Assert.Equal("current", body.Checks["core.migrations"]);
    }

    [Fact]
    public async Task Not_ready_with_503_when_migrations_are_pending()
    {
        var connection = await postgres.CreateDatabaseAsync(migrated: false);
        await using var factory = new TestApiFactory(connection);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("unhealthy", body.Status);
        Assert.Equal("up", body.Checks["core.database"]);
        Assert.Equal("pending", body.Checks["core.migrations"]);
    }

    [Fact]
    public async Task Not_ready_with_503_when_the_database_is_unreachable_and_nothing_leaks()
    {
        await using var factory = new TestApiFactory(TestApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<HealthResponse>(text, JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("down", body.Checks["core.database"]);
        Assert.DoesNotContain("127.0.0.1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("example-only", text, StringComparison.Ordinal);
        Assert.DoesNotContain("example_user", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Liveness_stays_200_even_when_the_database_is_down()
    {
        await using var factory = new TestApiFactory(TestApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
