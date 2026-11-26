using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using SaveSphere.TestSupport;

namespace SaveSphere.IntegrationTests;

/// <summary>
/// End-to-end proof of NFR-10 / SEC-01 for logging: secrets that code logs, request details and exception text
/// never reach the sink behind the platform's redaction chokepoint.
/// </summary>
[Requirement("NFR-10")]
[Requirement("SEC-01")]
public sealed class RedactionThroughTheApiTests
{
    private static TestApiFactory CreateFactory(CollectingSink sink) =>
        new(
            TestApiFactory.UnreachableDatabase,
            configureServices: services =>
            {
                TestEndpoints.Register(services);
                services.AddSingleton<ILogEventSink>(sink);
            });

    [Fact]
    public async Task Secrets_logged_by_application_code_are_masked()
    {
        var sink = new CollectingSink();
        await using var factory = CreateFactory(sink);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/test/log-secrets", UriKind.Relative),
            new { userName = "someone", passphrase = TestEndpoints.FakePassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var output = sink.RenderEverything();
        Assert.Contains("someone", output, StringComparison.Ordinal);
        Assert.DoesNotContain(TestEndpoints.FakePassword, output, StringComparison.Ordinal);
        Assert.DoesNotContain(TestEndpoints.FakeToken, output, StringComparison.Ordinal);
        Assert.DoesNotContain("db.example.invalid", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unhandled_exception_text_with_a_connection_string_is_masked_in_logs()
    {
        var sink = new CollectingSink();
        await using var factory = CreateFactory(sink);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/test/fault", UriKind.Relative));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var output = sink.RenderEverything();
        Assert.Contains("InvalidOperationException", output, StringComparison.Ordinal);
        Assert.DoesNotContain(TestEndpoints.FakePassword, output, StringComparison.Ordinal);
        Assert.DoesNotContain("db.example.invalid", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_logging_records_the_path_but_not_query_string_headers_or_body()
    {
        var sink = new CollectingSink();
        await using var factory = CreateFactory(sink);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(
            $"/api/v1/health/live?token={TestEndpoints.FakeToken}&password={TestEndpoints.FakePassword}", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + TestEndpoints.FakeToken);
        request.Headers.TryAddWithoutValidation("Cookie", "session=" + TestEndpoints.FakeToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var output = sink.RenderEverything();
        Assert.Contains("/api/v1/health/live", output, StringComparison.Ordinal);
        Assert.DoesNotContain(TestEndpoints.FakeToken, output, StringComparison.Ordinal);
        Assert.DoesNotContain(TestEndpoints.FakePassword, output, StringComparison.Ordinal);
    }
}
