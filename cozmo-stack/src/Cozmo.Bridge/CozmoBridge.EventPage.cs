using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    // Leave room for the JSON-RPC wrapper, tool text echo, and the "during" member.
    private const int CompactToolReplyBytes = 19750;
    private sealed record EventPage(long Cursor, long OldestCursor, bool Gap, FeedEvent[] Events);
    private sealed record Sightings(string Type, int Count, long FirstCursor, long LastCursor,
        DateTimeOffset FirstTimestampUtc, DateTimeOffset LastTimestampUtc,
        uint? FirstRobotTimestamp, uint? LastRobotTimestamp, JsonElement LastPayload);

    private long CurrentEventCursor()
    {
        lock (eventGate)
        {
            ThrowIfDisposed();
            return lastCursor;
        }
    }

    // Caller holds eventGate. A nonmatching event can advance the cursor but never
    // costs a slot in the requested page. Stop before a matching event beyond limit.
    private EventPage CaptureEventPage(long since, int limit, HashSet<string>? include, HashSet<string>? exclude)
    {
        ThrowIfDisposed();
        if (since > lastCursor)
            throw new ArgumentOutOfRangeException(nameof(since), "Cursor belongs to a different or newer event feed.");

        var selected = new List<FeedEvent>();
        long next = since;
        foreach (var item in events)
        {
            if (item.Cursor <= since) continue;
            if ((include is null || include.Contains(item.Type)) && exclude?.Contains(item.Type) != true)
            {
                if (selected.Count == limit) break;
                selected.Add(item);
            }
            next = item.Cursor;
        }
        long oldest = events.TryPeek(out var first) ? first.Cursor : 1;
        return new(next, oldest, since < oldest - 1, selected.ToArray());
    }

    /// <summary>Reads a filtered, paginated event feed for MCP. Waiting observes the same filter.</summary>
    public async Task<JsonElement> ReadEventPageAsync(long since, int waitMs, int limit,
        IReadOnlyCollection<string>? types, IReadOnlyCollection<string>? excludeTypes, bool full,
        CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(since);
        ArgumentOutOfRangeException.ThrowIfNegative(waitMs);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(waitMs, 30000);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var include = types?.ToHashSet(StringComparer.Ordinal);
        var exclude = excludeTypes?.ToHashSet(StringComparer.Ordinal);
        var clock = Stopwatch.StartNew();
        while (true)
        {
            EventPage page;
            Task signal;
            lock (eventGate)
            {
                page = CaptureEventPage(since, limit, include, exclude);
                signal = eventAvailable.Task;
            }
            var remaining = TimeSpan.FromMilliseconds(waitMs) - clock.Elapsed;
            if (page.Gap || page.Events.Length > 0 || remaining <= TimeSpan.Zero)
                return SerializeReadPage(page, full);
            try { await signal.WaitAsync(remaining, token).ConfigureAwait(false); }
            catch (TimeoutException) { }
        }
    }

    private void AttachDuring(JsonObject response, long since, bool full, uint? completionTag)
    {
        EventPage page;
        lock (eventGate) page = CaptureEventPage(since, int.MaxValue, null, null);
        int budget = full ? int.MaxValue : CompactToolReplyBytes - EchoPayloadCost(JsonSerializer.SerializeToElement(response, Json));
        response["during"] = JsonNode.Parse(FormatDuring(page, since, full, budget, completionTag).GetRawText());
    }

    private static bool IsSighting(string type) =>
        type is "face-seen" or "cube-seen" or "charger-seen" or "robot-state";

    private static (FeedEvent[] Remaining, Sightings[] Sightings) Collapse(FeedEvent[] events)
    {
        var sightings = events.Where(e => IsSighting(e.Type))
            .GroupBy(e => e.Type, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group =>
            {
                var first = group.First();
                var last = group.Last();
                return new Sightings(group.Key, group.Count(), first.Cursor, last.Cursor,
                    first.TimestampUtc, last.TimestampUtc, first.RobotTimestamp, last.RobotTimestamp, last.Payload);
            }).ToArray();
        var collapsed = sightings.Select(sighting => sighting.Type).ToHashSet(StringComparer.Ordinal);
        return (events.Where(e => !collapsed.Contains(e.Type)).ToArray(), sightings);
    }

    private static int EchoPayloadCost(JsonElement data)
    {
        string json = data.GetRawText();
        return Encoding.UTF8.GetByteCount(json) + JsonEncodedText.Encode(json).EncodedUtf8Bytes.Length;
    }

    private static JsonElement SerializeReadPage(EventPage page, bool full)
    {
        if (full) return JsonSerializer.SerializeToElement(page, Json);
        int keep = page.Events.Length;
        while (true)
        {
            bool all = keep == page.Events.Length;
            var (remaining, sightings) = Collapse(all ? page.Events : page.Events[..keep]);
            long cursor = all ? page.Cursor : page.Events[keep - 1].Cursor;
            var result = JsonSerializer.SerializeToElement(new
            {
                cursor, page.OldestCursor, page.Gap, events = remaining, sightings,
            }, Json);
            if (keep <= 1 || EchoPayloadCost(result) <= CompactToolReplyBytes) return result;
            keep /= 2;
        }
    }

    private static JsonElement FormatDuring(EventPage page, long since, bool full, int budget, uint? completionTag)
    {
        if (full) return JsonSerializer.SerializeToElement(page, Json);
        var (remaining, sightings) = Collapse(page.Events);
        int keep = remaining.Length;
        var complete = JsonSerializer.SerializeToElement(new
        {
            page.Cursor, page.OldestCursor, page.Gap, events = remaining, sightings,
        }, Json);
        if (keep == 0 || EchoPayloadCost(complete) <= budget) return complete;

        var completion = completionTag is { } tag ? remaining.LastOrDefault(e => e.Type == "action-completed" &&
            e.Payload.GetProperty("completion").GetProperty("tag").GetUInt32() == tag) : null;

        // The first reply covers the full action window for sighting counts. Other
        // events stay in their original shape and can be paged from the same feed.
        // Keep the requested put-down's completion in the reply even after slicing.
        string[] excludeTypes = sightings.Select(sighting => sighting.Type).ToArray();
        while (true)
        {
            keep /= 2;
            var prefix = remaining[..keep];
            var visible = completion is not null && !prefix.Contains(completion) ? [..prefix, completion] : prefix;
            var result = JsonSerializer.SerializeToElement(new
            {
                page.Cursor, page.OldestCursor, page.Gap, events = visible, sightings,
                continuation = new { since = keep == 0 ? since : prefix[^1].Cursor, through = page.Cursor, excludeTypes },
            }, Json);
            if (keep == 0 || EchoPayloadCost(result) <= budget) return result;
        }
    }
}
