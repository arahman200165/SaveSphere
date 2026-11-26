using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SaveSphere.TestSupport;

namespace SaveSphere.IntegrationTests;

/// <summary>ADR-0002: endpoint prefix, kebab-case routes, generated contract, problem details.</summary>
[Requirement("ARC-08")]
public sealed partial class ApiConventionTests
{
    private const string ContractPath = "packages/api-contracts/openapi.json";

    [Fact]
    public async Task Every_endpoint_lives_under_api_v1_with_kebab_case_static_segments()
    {
        await using var factory = new TestApiFactory(TestApiFactory.UnreachableDatabase, configureServices: TestEndpoints.Register);
        using var client = factory.CreateClient(); // forces the host (and endpoint mapping) to start

        var endpoints = factory.Services.GetServices<EndpointDataSource>().SelectMany(s => s.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .ToList();

        Assert.NotEmpty(endpoints);
        foreach (var pattern in endpoints)
        {
            Assert.StartsWith("/api/v1/", pattern, StringComparison.Ordinal);
            foreach (var segment in pattern.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(2))
            {
                if (segment.Contains('{', StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.Matches(KebabCase(), segment);
            }
        }
    }

    [Fact]
    public async Task The_committed_openapi_contract_matches_the_generated_document()
    {
        await using var factory = new TestApiFactory(TestApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        var generated = await client.GetStringAsync(new Uri("/api/v1/openapi/v1.json", UriKind.Relative));
        var generatedNode = JsonNode.Parse(generated)!;
        var path = RepositoryPaths.Combine(ContractPath.Split('/'));

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, generatedNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

        Assert.True(File.Exists(path), $"{ContractPath} is missing. Run the tests once with UPDATE_OPENAPI=1 and commit the file.");
        var committed = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        Assert.True(
            JsonNode.DeepEquals(committed, generatedNode),
            $"{ContractPath} is stale. Regenerate it with UPDATE_OPENAPI=1 and commit the result.");
    }

    [Fact]
    public async Task Unknown_routes_return_problem_details()
    {
        await using var factory = new TestApiFactory(TestApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(404, problem?.Status);
    }

    [Fact]
    public async Task Unhandled_exceptions_return_generic_problem_details_without_internals_in_production()
    {
        await using var factory = new TestApiFactory(TestApiFactory.UnreachableDatabase, configureServices: TestEndpoints.Register);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/test/fault", UriKind.Relative));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(TestEndpoints.FakeConnectionString, text, StringComparison.Ordinal);
        Assert.DoesNotContain("example-pw", text, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", text, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("TestEndpoints", text, StringComparison.Ordinal);
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KebabCase();
}
