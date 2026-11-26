using Serilog;
using Serilog.Events;
using SaveSphere.Infrastructure.Logging;
using SaveSphere.SharedKernel;
using SaveSphere.TestSupport;

namespace SaveSphere.UnitTests;

/// <summary>
/// Every value below is an obviously fake placeholder. The assertions prove none of them ever reaches a sink.
/// </summary>
[Requirement("NFR-10")]
[Requirement("SEC-01")]
public sealed class LogRedactionTests
{
    private const string FakePassword = "fake-pw-0000";
    private const string FakeToken = "fake-token-0000";
    private const string FakeConnectionString =
        "Host=db.example.invalid;Port=5432;Database=app_example;Username=app_user;Password=" + FakePassword;

    private static (ILogger Logger, CollectingSink Sink) CreateLogger()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .ConfigureSaveSphere([sink])
            .CreateLogger();
        return (logger, sink);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("db_password")]
    [InlineData("ApiKey")]
    [InlineData("X-Api-Key")]
    [InlineData("Authorization")]
    [InlineData("accessToken")]
    [InlineData("client_secret")]
    [InlineData("ConnectionString")]
    [InlineData("Set-Cookie")]
    [InlineData("RecoveryCode")]
    public void Sensitive_names_are_recognised(string name) => Assert.True(LogRedactor.IsSensitiveName(name));

    [Theory]
    [InlineData("RequestPath")]
    [InlineData("StatusCode")]
    [InlineData("Elapsed")]
    [InlineData("Module")]
    public void Ordinary_names_are_not_masked(string name) => Assert.False(LogRedactor.IsSensitiveName(name));

    [Theory]
    [InlineData("Authorization: Bearer " + FakeToken)]
    [InlineData("failed with " + FakeConnectionString)]
    [InlineData("password=" + FakePassword + "; other=1")]
    [InlineData("pwd=" + FakePassword)]
    [InlineData("Basic ZmFrZTpmYWtl")]
    [InlineData("jwt eyJmYWtl.eyJmYWtlIjoxfQ.c2ln end")]
    public void Free_text_is_scrubbed(string text)
    {
        var scrubbed = LogRedactor.ScrubText(text);

        Assert.DoesNotContain(FakePassword, scrubbed, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeToken, scrubbed, StringComparison.Ordinal);
        Assert.DoesNotContain("db.example.invalid", scrubbed, StringComparison.Ordinal);
        Assert.DoesNotContain("ZmFrZTpmYWtl", scrubbed, StringComparison.Ordinal);
        Assert.DoesNotContain("eyJmYWtl", scrubbed, StringComparison.Ordinal);
        Assert.Contains(LogRedactor.Mask, scrubbed, StringComparison.Ordinal);
    }

    [Fact]
    public void Harmless_text_is_untouched()
    {
        const string text = "HTTP GET /api/v1/health responded 200 in 1.2 ms";

        Assert.Equal(text, LogRedactor.ScrubText(text));
    }

    [Fact]
    public void Properties_with_sensitive_names_are_masked_wherever_they_appear()
    {
        var (logger, sink) = CreateLogger();

        logger.Information("Login {Password} {Authorization}", FakePassword, "Bearer " + FakeToken);
        logger.Information("Nested {@Payload}", new { User = "someone", ApiKey = FakeToken, Items = new[] { new { Secret = FakePassword } } });
        logger.Information("Dict {@Map}", new Dictionary<string, string> { ["client_secret"] = FakePassword, ["name"] = "ok" });

        var output = sink.RenderEverything();
        Assert.DoesNotContain(FakePassword, output, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeToken, output, StringComparison.Ordinal);
        Assert.Contains("someone", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Values_inside_benign_property_names_are_still_scrubbed()
    {
        var (logger, sink) = CreateLogger();

        logger.Information("Connecting with {Target}", FakeConnectionString);

        var output = sink.RenderEverything();
        Assert.DoesNotContain(FakePassword, output, StringComparison.Ordinal);
        Assert.DoesNotContain("db.example.invalid", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Message_template_text_is_scrubbed_when_a_secret_was_interpolated_into_it()
    {
        var (logger, sink) = CreateLogger();

#pragma warning disable CA2254, CA2017 // Deliberately logging an interpolated secret to prove the template is scrubbed.
        logger.Information($"Using {FakeConnectionString} now");
#pragma warning restore CA2254, CA2017

        Assert.DoesNotContain(FakePassword, sink.RenderEverything(), StringComparison.Ordinal);
    }

    [Fact]
    public void Exception_text_never_reaches_the_sink_unscrubbed()
    {
        var (logger, sink) = CreateLogger();
        var exception = new InvalidOperationException("Cannot connect: " + FakeConnectionString);

        logger.Error(exception, "Operation failed");

        var logEvent = Assert.Single(sink.Events);
        Assert.NotNull(logEvent.Exception);
        var output = sink.RenderEverything();
        Assert.DoesNotContain(FakePassword, output, StringComparison.Ordinal);
        Assert.DoesNotContain("db.example.invalid", output, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", logEvent.Exception.ToString(), StringComparison.Ordinal);
    }

    private sealed record LoginAttempt(string UserName, [property: Sensitive] string Passphrase);

    [Fact]
    public void Properties_marked_sensitive_are_masked_when_destructured()
    {
        var (logger, sink) = CreateLogger();

        logger.Information("Attempt {@Attempt}", new LoginAttempt("someone", FakePassword));

        var output = sink.RenderEverything();
        Assert.DoesNotContain(FakePassword, output, StringComparison.Ordinal);
        Assert.Contains("someone", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Redacted_wrapper_never_reveals_its_value_in_logs_or_strings()
    {
        var (logger, sink) = CreateLogger();
        var secret = new Redacted<string>(FakePassword);

        logger.Information("Wrapped {Value} and {@Structured}", secret, new { Inner = secret });

        Assert.Equal(Redacted<string>.Mask, secret.ToString());
        Assert.DoesNotContain(FakePassword, sink.RenderEverything(), StringComparison.Ordinal);
        Assert.Equal(FakePassword, secret.Reveal());
    }

    [Fact]
    public void Redacting_sink_preserves_level_timestamp_and_harmless_properties()
    {
        var (logger, sink) = CreateLogger();

        logger.Warning("Slow request {RequestPath} took {Elapsed} ms", "/api/v1/health", 12);

        var logEvent = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Warning, logEvent.Level);
        Assert.Equal("/api/v1/health", ((ScalarValue)logEvent.Properties["RequestPath"]).Value);
        Assert.Equal(12, ((ScalarValue)logEvent.Properties["Elapsed"]).Value);
    }
}
