using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace SaveSphere.Infrastructure.Logging;

/// <summary>
/// Wraps the real sink(s) so that no log event reaches output without being scrubbed:
/// property values, message template text and exception text (ADR-0002, NFR-10).
/// </summary>
public sealed class RedactingSink(ILogEventSink inner) : ILogEventSink
{
    private static readonly MessageTemplateParser TemplateParser = new();

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var properties = logEvent.Properties.Select(p => new LogEventProperty(
            p.Key,
            LogRedactor.IsSensitiveName(p.Key) ? new ScalarValue(LogRedactor.Mask) : LogRedactor.Scrub(p.Value)));

        var template = TemplateParser.Parse(LogRedactor.ScrubText(logEvent.MessageTemplate.Text));
        var exception = logEvent.Exception is null ? null : new RedactedException(logEvent.Exception);

        inner.Emit(new LogEvent(
            logEvent.Timestamp,
            logEvent.Level,
            exception,
            template,
            properties,
            logEvent.TraceId ?? default,
            logEvent.SpanId ?? default));
    }
}

/// <summary>Carries only scrubbed exception text; the original exception (and its data) never reaches a sink.</summary>
internal sealed class RedactedException(Exception original) : Exception(LogRedactor.ScrubText(original.Message))
{
    private readonly string _text = LogRedactor.ScrubText(original.ToString());

    public override string ToString() => _text;
}
