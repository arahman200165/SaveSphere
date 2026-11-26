using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Serilog.Core;
using Serilog.Events;

namespace SaveSphere.TestSupport;

/// <summary>Captures log events that reached a sink, and renders everything a log reader could see.</summary>
public sealed class CollectingSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyList<LogEvent> Events => [.. _events];

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    /// <summary>Message, template, every property value and exception text of all captured events, as one string.</summary>
    public string RenderEverything()
    {
        var text = new StringBuilder();
        foreach (var logEvent in _events)
        {
            text.AppendLine(logEvent.RenderMessage(CultureInfo.InvariantCulture));
            text.AppendLine(logEvent.MessageTemplate.Text);
            foreach (var property in logEvent.Properties)
            {
                text.Append(property.Key).Append('=').AppendLine(property.Value.ToString());
            }

            if (logEvent.Exception is not null)
            {
                text.AppendLine(logEvent.Exception.ToString());
                text.AppendLine(logEvent.Exception.Message);
            }
        }

        return text.ToString();
    }
}
