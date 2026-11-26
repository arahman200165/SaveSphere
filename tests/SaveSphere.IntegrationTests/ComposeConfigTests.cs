using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SaveSphere.TestSupport;

namespace SaveSphere.IntegrationTests;

/// <summary>
/// ADR-0004 evidence: dev and prod are isolated, nothing but loopback is published, the start order is
/// postgres -> migrator -> api, and no secret is committed or defaulted. Runs `docker compose config`
/// with a throwaway placeholder password; nothing is started.
/// </summary>
[Requirement("NFR-10")]
[Requirement("NFR-07")]
public sealed partial class ComposeConfigTests
{
    private const string PlaceholderPassword = "placeholder-not-a-secret";

    private static readonly Lazy<JsonObject> Dev = new(() => Render("docker-compose.dev.yml"));
    private static readonly Lazy<JsonObject> Prod = new(() => Render("docker-compose.prod.yml"));

    private static (int ExitCode, string Output) RunCompose(string[] arguments, string? password)
    {
        var start = new ProcessStartInfo("docker")
        {
            WorkingDirectory = RepositoryPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment.Remove("POSTGRES_PASSWORD");
        start.Environment.Remove("POSTGRES_HOST_PORT");
        start.Environment.Remove("API_HOST_PORT");
        if (password is not null)
        {
            start.Environment["POSTGRES_PASSWORD"] = password;
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    private static JsonObject Render(string overrideFile)
    {
        var (exitCode, output) = RunCompose(
            ["compose", "-f", "docker-compose.yml", "-f", overrideFile, "config", "--format", "json"],
            PlaceholderPassword);
        Assert.True(exitCode == 0, $"docker compose config failed for {overrideFile}: {output}");
        return JsonNode.Parse(output)!.AsObject();
    }

    private static void RequireCompose()
    {
        try
        {
            var (exitCode, _) = RunCompose(["compose", "version"], null);
            Assert.SkipUnless(exitCode == 0, "docker compose is not available");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Assert.Skip("docker is not installed");
        }
    }

    private static JsonObject Service(JsonObject config, string name) => config["services"]![name]!.AsObject();

    private static IEnumerable<(string Service, JsonObject Port)> PublishedPorts(JsonObject config) =>
        config["services"]!.AsObject().SelectMany(s =>
            (s.Value!["ports"]?.AsArray() ?? []).Select(p => (s.Key, p!.AsObject())));

    [Fact]
    public void Dev_and_prod_share_no_project_volume_network_database_or_role()
    {
        RequireCompose();

        Assert.Equal("savesphere-dev", Dev.Value["name"]!.GetValue<string>());
        Assert.Equal("savesphere-prod", Prod.Value["name"]!.GetValue<string>());

        Assert.NotEqual(
            Dev.Value["volumes"]!["pgdata"]!["name"]!.GetValue<string>(),
            Prod.Value["volumes"]!["pgdata"]!["name"]!.GetValue<string>());
        Assert.NotEqual(
            Dev.Value["networks"]!["default"]!["name"]!.GetValue<string>(),
            Prod.Value["networks"]!["default"]!["name"]!.GetValue<string>());

        string[] identifiers = ["POSTGRES_DB", "POSTGRES_USER"];
        foreach (var key in identifiers)
        {
            Assert.NotEqual(
                Service(Dev.Value, "postgres")["environment"]![key]!.GetValue<string>(),
                Service(Prod.Value, "postgres")["environment"]![key]!.GetValue<string>());
        }
    }

    [Fact]
    public void Dev_and_prod_cannot_collide_on_host_ports()
    {
        RequireCompose();

        var dev = PublishedPorts(Dev.Value).Select(p => p.Port["published"]!.GetValue<string>()).ToHashSet();
        var prod = PublishedPorts(Prod.Value).Select(p => p.Port["published"]!.GetValue<string>()).ToHashSet();

        Assert.NotEmpty(dev);
        Assert.NotEmpty(prod);
        Assert.Empty(dev.Intersect(prod));
    }

    [Fact]
    public void Every_published_port_is_bound_to_loopback_only()
    {
        RequireCompose();

        foreach (var config in new[] { Dev.Value, Prod.Value })
        {
            foreach (var (service, port) in PublishedPorts(config))
            {
                Assert.True(
                    port["host_ip"]?.GetValue<string>() == "127.0.0.1",
                    $"{service} publishes {port["published"]} on {port["host_ip"]?.GetValue<string>() ?? "all interfaces"}");
            }
        }
    }

    [Fact]
    public void Production_postgres_publishes_no_port_and_only_the_api_is_reachable_from_the_host()
    {
        RequireCompose();

        var services = PublishedPorts(Prod.Value).Select(p => p.Service).Distinct().ToList();

        Assert.Equal(["api"], services);
    }

    [Fact]
    public void Postgres_image_is_pinned_to_an_exact_minor_and_matches_the_test_container()
    {
        RequireCompose();

        foreach (var config in new[] { Dev.Value, Prod.Value })
        {
            var image = Service(config, "postgres")["image"]!.GetValue<string>();
            Assert.Matches(@"^postgres:\d+\.\d+$", image);
            Assert.Equal(PostgresFixture.ImageTag, image);
        }
    }

    [Fact]
    public void Start_order_is_postgres_healthy_then_migrator_completed_then_api()
    {
        RequireCompose();

        foreach (var config in new[] { Dev.Value, Prod.Value })
        {
            Assert.Equal("service_healthy", Service(config, "migrator")["depends_on"]!["postgres"]!["condition"]!.GetValue<string>());
            Assert.Equal("service_completed_successfully", Service(config, "api")["depends_on"]!["migrator"]!["condition"]!.GetValue<string>());
            Assert.Equal("no", Service(config, "migrator")["restart"]!.GetValue<string>());
        }
    }

    [Fact]
    public void App_containers_run_read_only_with_all_capabilities_dropped_and_no_new_privileges()
    {
        RequireCompose();

        foreach (var config in new[] { Dev.Value, Prod.Value })
        {
            foreach (var name in new[] { "api", "migrator" })
            {
                var service = Service(config, name);
                Assert.True(service["read_only"]!.GetValue<bool>(), $"{name} must have a read-only root filesystem");
                Assert.Contains("ALL", service["cap_drop"]!.AsArray().Select(c => c!.GetValue<string>()));
                Assert.Contains("no-new-privileges:true", service["security_opt"]!.AsArray().Select(c => c!.GetValue<string>()));
            }
        }
    }

    [Fact]
    public void Rendering_without_a_password_fails_so_there_is_no_fallback_secret()
    {
        RequireCompose();

        var (exitCode, output) = RunCompose(
            ["compose", "-f", "docker-compose.yml", "-f", "docker-compose.dev.yml", "config", "--quiet"],
            password: null);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("POSTGRES_PASSWORD", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Committed_compose_files_contain_no_literal_password()
    {
        foreach (var file in new[] { "docker-compose.yml", "docker-compose.dev.yml", "docker-compose.prod.yml" })
        {
            var text = File.ReadAllText(RepositoryPaths.Combine(file));
            foreach (Match match in PasswordAssignment().Matches(text))
            {
                Assert.True(
                    match.Groups[1].Value.StartsWith("${POSTGRES_PASSWORD", StringComparison.Ordinal),
                    $"{file}: password must come from the environment, found '{match.Value}'");
            }
        }
    }

    [GeneratedRegex(@"(?i)password\s*[=:]\s*([^;""\s]+)")]
    private static partial Regex PasswordAssignment();
}
