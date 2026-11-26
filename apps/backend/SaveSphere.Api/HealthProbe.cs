namespace SaveSphere.Api;

/// <summary>
/// Container health check: "SaveSphere.Api.dll --healthcheck" calls the liveness endpoint on loopback and exits
/// 0 or 1. The ASP.NET runtime image ships no curl or wget, so the app probes itself.
/// </summary>
internal static class HealthProbe
{
    public static async Task<int> RunAsync()
    {
        var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?
            .Split(';', StringSplitOptions.RemoveEmptyEntries)[0] ?? "8080";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var response = await client.GetAsync(new Uri($"http://127.0.0.1:{port}/api/v1/health/live"));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return 1;
        }
    }
}
