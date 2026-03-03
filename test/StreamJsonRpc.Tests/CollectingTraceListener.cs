using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;

public class CollectingTraceListener : TraceListener
{
    private readonly StringBuilder lineInProgress = new StringBuilder();

    private readonly ImmutableList<string>.Builder messages = ImmutableList.CreateBuilder<string>();

    private readonly ImmutableList<JsonRpc.TraceEvents>.Builder traceEventIds = ImmutableList.CreateBuilder<JsonRpc.TraceEvents>();

    private readonly ImmutableList<(TraceEventType EventType, string? Message)>.Builder events = ImmutableList.CreateBuilder<(TraceEventType EventType, string? Message)>();

    private readonly ImmutableList<(Guid RelatedActivityId, Guid CurrentActivityId)>.Builder transfers = ImmutableList.CreateBuilder<(Guid RelatedActivityId, Guid CurrentActivityId)>();

    private readonly ImmutableList<(JsonRpc.TraceEvents TraceEventId, Guid ActivityId)>.Builder eventsWithActivityIds = ImmutableList.CreateBuilder<(JsonRpc.TraceEvents TraceEventId, Guid ActivityId)>();

    public override bool IsThreadSafe => false;

    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (this.messages)
            {
                return this.messages.ToImmutable();
            }
        }
    }

    public ImmutableList<JsonRpc.TraceEvents> Ids
    {
        get
        {
            lock (this.traceEventIds)
            {
                return this.traceEventIds.ToImmutable();
            }
        }
    }

    public ImmutableList<(TraceEventType EventType, string? Message)> Events
    {
        get
        {
            lock (this.events)
            {
                return this.events.ToImmutable();
            }
        }
    }

    public ImmutableList<(Guid RelatedActivityId, Guid CurrentActivityId)> Transfers
    {
        get
        {
            lock (this.transfers)
            {
                return this.transfers.ToImmutable();
            }
        }
    }

    public ImmutableList<(JsonRpc.TraceEvents TraceEventId, Guid ActivityId)> EventsWithActivityIds
    {
        get
        {
            lock (this.eventsWithActivityIds)
            {
                return this.eventsWithActivityIds.ToImmutable();
            }
        }
    }

    public AsyncAutoResetEvent MessageReceived { get; } = new AsyncAutoResetEvent();

    public override void TraceTransfer(TraceEventCache? eventCache, string source, int id, string? message, Guid relatedActivityId)
    {
        base.TraceTransfer(eventCache, source, id, message, relatedActivityId);

        lock (this.transfers)
        {
            this.transfers.Add((relatedActivityId, Trace.CorrelationManager.ActivityId));
        }
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
    {
        lock (this.traceEventIds)
        {
            this.traceEventIds.Add((JsonRpc.TraceEvents)id);
        }

        lock (this.events)
        {
            this.events.Add((eventType, format is null ? null : string.Format(CultureInfo.InvariantCulture, format, args ?? Array.Empty<object?>())));
        }

        lock (this.eventsWithActivityIds)
        {
            this.eventsWithActivityIds.Add(((JsonRpc.TraceEvents)id, Trace.CorrelationManager.ActivityId));
        }

        base.TraceEvent(eventCache, source, eventType, id, format, args);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
    {
        lock (this.traceEventIds)
        {
            this.traceEventIds.Add((JsonRpc.TraceEvents)id);
        }

        lock (this.events)
        {
            this.events.Add((eventType, message));
        }

        lock (this.eventsWithActivityIds)
        {
            this.eventsWithActivityIds.Add(((JsonRpc.TraceEvents)id, Trace.CorrelationManager.ActivityId));
        }

        base.TraceEvent(eventCache, source, eventType, id, message);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id)
    {
        lock (this.traceEventIds)
        {
            this.traceEventIds.Add((JsonRpc.TraceEvents)id);
        }

        lock (this.events)
        {
            this.events.Add((eventType, null));
        }

        lock (this.eventsWithActivityIds)
        {
            this.eventsWithActivityIds.Add(((JsonRpc.TraceEvents)id, Trace.CorrelationManager.ActivityId));
        }

        base.TraceEvent(eventCache, source, eventType, id);
    }

    public override void Write(string? message) => this.lineInProgress.Append(message);

    public override void WriteLine(string? message)
    {
        this.lineInProgress.Append(message);
        lock (this.messages)
        {
            this.messages.Add(this.lineInProgress.ToString());
            this.MessageReceived.Set();
        }

        this.lineInProgress.Clear();
    }
}
