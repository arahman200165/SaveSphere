using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace SaveSphere.Infrastructure.Logging;

public static class SaveSphereLogging
{
    /// <summary>
    /// Structured JSON logging to the console with the central redaction chokepoint (ADR-0002).
    /// Every sink, including <paramref name="additionalSinks"/> (used by tests), sits behind <see cref="RedactingSink"/>.
    /// </summary>
    public static LoggerConfiguration ConfigureSaveSphere(
        this LoggerConfiguration configuration,
        IEnumerable<ILogEventSink>? additionalSinks = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var inner = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(new RenderedCompactJsonFormatter());

        foreach (var sink in additionalSinks ?? [])
        {
            inner.WriteTo.Sink(sink);
        }

        return configuration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Destructure.With<SensitiveDestructuringPolicy>()
            .WriteTo.Sink(new RedactingSink(inner.CreateLogger()));
    }
}
