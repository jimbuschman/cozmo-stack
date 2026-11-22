using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    // Leave room for the JSON-RPC wrapper, tool text echo, and the "during" member.
    private const int CompactToolReplyBytes = 19750;
    private sealed record Sightings(string Type, int Count, long FirstCursor, long LastCursor,
        DateTimeOffset FirstTimestampUtc, DateTimeOffset LastTimestampUtc,
        uint? FirstRobotTimestamp, uint? LastRobotTimestamp, JsonElement LastPayload,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FaceId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name);
    private sealed record AnimationWindow(byte Tag, string? Name, long StartCursor, long EndCursor,
        DateTimeOffset StartUtc, DateTimeOffset EndUtc, string Result);

    private static (BridgeEvent[] Events, AnimationWindow[] Animations) CollapseAnimations(BridgeEvent[] events)
    {
        var starts = new Dictionary<byte, BridgeEvent>();
        var paired = new HashSet<long>();
        var summaries = new List<AnimationWindow>();
        foreach (var entry in events)
        {
            if (entry.Type is not ("animation-started" or "animation-ended" or "animation-aborted")) continue;
            byte tag = entry.Payload.GetProperty("tag").GetByte();
            if (entry.Type == "animation-started")
            {
                starts[tag] = entry;
                continue;
            }
            if (!starts.Remove(tag, out var start)) continue;

            paired.Add(start.Cursor);
            paired.Add(entry.Cursor);
            string? name = entry.Payload.GetProperty("name").GetString() ?? start.Payload.GetProperty("name").GetString();
            summaries.Add(new(tag, name, start.Cursor, entry.Cursor, start.TimestampUtc, entry.TimestampUtc,
                entry.Type == "animation-ended" ? "ended" : "aborted"));
        }
        return (events.Where(e => !paired.Contains(e.Cursor)).ToArray(),
            summaries.OrderBy(summary => summary.StartCursor).ToArray());
    }

    private readonly record struct SightingKey(string Type, int? FaceId = null, string? Name = null);

    internal long CurrentEventCursor()
    {
        lock (eventGate)
        {
            ThrowIfDisposed();
            return lastCursor;
        }
    }

    // Caller holds eventGate. A nonmatching event can advance the cursor but never
    // costs a slot in the requested page. Stop before a matching event beyond limit.
    private enum EventView { Full, Compact, Push }

    private BridgeEventPage CaptureEventPage(long since, int limit, HashSet<string>? include, HashSet<string>? exclude,
        bool significantOnly = false, EventView view = EventView.Full)
    {
        ThrowIfDisposed();
        if (since > lastCursor)
            throw new ArgumentOutOfRangeException(nameof(since), "Cursor belongs to a different or newer event feed.");

        var selected = new List<BridgeEvent>();
        long next = since;
        foreach (var entry in events)
        {
            var item = entry.Full;
            if (item.Cursor <= since) continue;
            bool significant = view switch
            {
                EventView.Push => entry.PushSignificant,
                EventView.Compact => entry.CompactSignificant,
                _ => item.Significant,
            };
            if ((!significantOnly || significant) &&
                (include is null || include.Contains(item.Type)) && exclude?.Contains(item.Type) != true)
            {
                if (selected.Count == limit) break;
                selected.Add(view == EventView.Full ? item : item with { Payload = entry.CompactPayload ?? item.Payload, Significant = significant });
            }
            next = item.Cursor;
        }
        long oldest = events.TryPeek(out var first) ? first.Full.Cursor : 1;
        return new(next, oldest, since < oldest - 1, selected.ToArray());
    }

    /// <summary>Reads a filtered, paginated event feed for MCP. Waiting observes the same filter.</summary>
    public async Task<JsonElement> ReadEventPageAsync(long since, int waitMs, int limit,
        IReadOnlyCollection<string>? types, IReadOnlyCollection<string>? excludeTypes, bool full,
        CancellationToken token = default, bool significantOnly = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(since);
        ArgumentOutOfRangeException.ThrowIfNegative(waitMs);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(waitMs, 30000);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var include = types?.ToHashSet(StringComparer.Ordinal);
        var exclude = excludeTypes?.ToHashSet(StringComparer.Ordinal);
        var page = await WaitEventPageAsync(since, waitMs, limit, include, exclude, significantOnly, token,
            full ? EventView.Full : EventView.Compact).ConfigureAwait(false);
        return SerializeReadPage(page, full, significantOnly);
    }

    private async Task<BridgeEventPage> WaitEventPageAsync(long since, int waitMs, int limit,
        HashSet<string>? include, HashSet<string>? exclude, bool significantOnly, CancellationToken token,
        EventView view = EventView.Full)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            BridgeEventPage page;
            Task signal;
            lock (eventGate)
            {
                page = CaptureEventPage(since, limit, include, exclude, significantOnly, view);
                signal = eventAvailable.Task;
            }
            var remaining = TimeSpan.FromMilliseconds(waitMs) - clock.Elapsed;
            if (page.Gap || page.Events.Count > 0 || remaining <= TimeSpan.Zero) return page;
            since = page.Cursor;
            try { await signal.WaitAsync(remaining, token).ConfigureAwait(false); }
            catch (TimeoutException) { }
        }
    }

    private async Task WaitForOwnedCompletion(long since, uint tag)
    {
        // The action task can finish before the queued ActionCompleted reaches the event feed.
        while (true)
        {
            Task signal;
            lock (eventGate)
            {
                if (disposed) return;
                var page = CaptureEventPage(since, int.MaxValue, null, null);
                if (page.Gap || page.Events.Any(e => IsCompletionFor(e, tag))) return;
                since = page.Cursor;
                signal = eventAvailable.Task;
            }
            await signal.ConfigureAwait(false);
        }
    }

    private static bool IsCompletionFor(BridgeEvent entry, uint tag) => entry.Type == "action-completed" &&
        entry.Payload.GetProperty("completion").GetProperty("tag").GetUInt32() == tag;

    private void AttachDuring(JsonObject response, long since, bool full, uint? completionTag)
    {
        BridgeEventPage page;
        lock (eventGate) page = CaptureEventPage(since, int.MaxValue, null, null,
            view: full ? EventView.Full : EventView.Compact);
        int budget = full ? int.MaxValue : CompactToolReplyBytes - EchoPayloadCost(JsonSerializer.SerializeToElement(response, Json));
        response["during"] = JsonNode.Parse(FormatDuring(page, since, full, budget, completionTag).GetRawText());
    }

    private static bool IsSighting(string type) =>
        type is "face-seen" or "cube-seen" or "charger-seen" or "robot-state";

    private static (BridgeEvent[] Remaining, Sightings[] Sightings) Collapse(BridgeEvent[] events)
    {
        var candidates = new Dictionary<SightingKey, List<BridgeEvent>>();
        var previousName = new Dictionary<int, string>();
        foreach (var item in events)
        {
            SightingKey key = new(item.Type);
            if (item.Type == "face-recognised")
            {
                if (item.Payload.ValueKind != JsonValueKind.Object ||
                    !item.Payload.TryGetProperty("faceId", out var id) || id.ValueKind != JsonValueKind.Number ||
                    !id.TryGetInt32(out int faceId) ||
                    !item.Payload.TryGetProperty("name", out var field) || field.ValueKind != JsonValueKind.String ||
                    field.GetString() is not { Length: > 0 } name)
                    continue;

                // A first recognition or any name change stays in events, even if
                // the same face/name pair appeared earlier in the window.
                string? before = previousName.GetValueOrDefault(faceId);
                previousName[faceId] = name;
                if (!string.Equals(before, name, StringComparison.Ordinal)) continue;
                key = new(item.Type, faceId, name);
            }
            else if (!IsSighting(item.Type)) continue;

            if (!candidates.TryGetValue(key, out var group)) candidates.Add(key, group = []);
            group.Add(item);
        }

        var collapsed = new HashSet<long>();
        var sightings = new List<Sightings>();
        foreach (var (key, group) in candidates)
        {
            if (group.Count == 1 && key.FaceId is null) continue;
            var first = group[0];
            var last = group[^1];
            sightings.Add(new(key.Type, group.Count, first.Cursor, last.Cursor,
                first.TimestampUtc, last.TimestampUtc, first.RobotTimestamp, last.RobotTimestamp,
                last.Payload, key.FaceId, key.Name));
            foreach (var item in group) collapsed.Add(item.Cursor);
        }
        return (events.Where(e => !collapsed.Contains(e.Cursor)).ToArray(), sightings.ToArray());
    }

    private static int EchoPayloadCost(JsonElement data, JsonElement? text = null) =>
        Encoding.UTF8.GetByteCount(data.GetRawText()) +
        JsonEncodedText.Encode((text ?? data).GetRawText()).EncodedUtf8Bytes.Length;

    private static bool HasRecognitionSummaries(JsonElement during) =>
        during.TryGetProperty("sightings", out var sightings) &&
        sightings.EnumerateArray().Any(row => row.GetProperty("type").GetString() == "face-recognised");

    private static JsonElement RecognitionWindowText(JsonElement during) => JsonSerializer.SerializeToElement(new
    {
        cursor = during.GetProperty("cursor"),
        oldestCursor = during.GetProperty("oldestCursor"),
        gap = during.GetProperty("gap"),
        events = during.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString()!.StartsWith("face-", StringComparison.Ordinal)).ToArray(),
        sightings = during.GetProperty("sightings"),
        detailsInStructuredContent = true,
    }, Json);

    // Keep the full compact window in structuredContent. For a large recognition
    // window, the text echo still carries the action result and identity changes.
    internal static string ToolResultText(JsonElement result)
    {
        if (!result.TryGetProperty("during", out var during) || during.TryGetProperty("continuation", out _) ||
            !HasRecognitionSummaries(during) || EchoPayloadCost(result) <= CompactToolReplyBytes)
            return result.GetRawText();

        var text = JsonNode.Parse(result.GetRawText())!.AsObject();
        text["during"] = JsonNode.Parse(RecognitionWindowText(during).GetRawText());
        return text.ToJsonString(Json);
    }

    private static JsonElement SerializeReadPage(BridgeEventPage page, bool full, bool significantOnly)
    {
        if (full) return JsonSerializer.SerializeToElement(page, Json);
        var events = page.Events.ToArray();
        int keep = events.Length;
        while (true)
        {
            bool all = keep == events.Length;
            var prefix = all ? events : events[..keep];
            var (remaining, sightings) = significantOnly ? (prefix, Array.Empty<Sightings>()) : Collapse(prefix);
            long cursor = all ? page.Cursor : page.Events[keep - 1].Cursor;
            var result = JsonSerializer.SerializeToElement(new
            {
                cursor, page.OldestCursor, page.Gap, events = remaining, sightings,
            }, Json);
            if (keep <= 1 || EchoPayloadCost(result) <= CompactToolReplyBytes) return result;
            keep /= 2;
        }
    }

    private static JsonElement FormatDuring(BridgeEventPage page, long since, bool full, int budget, uint? completionTag)
    {
        if (full) return JsonSerializer.SerializeToElement(page, Json);
        var (ordinary, sightings) = Collapse(page.Events.ToArray());
        var (remaining, animations) = CollapseAnimations(ordinary);
        int keep = remaining.Length;
        var complete = JsonSerializer.SerializeToElement(new
        {
            page.Cursor, page.OldestCursor, page.Gap, events = remaining, sightings, animations,
        }, Json);
        if (keep == 0 || EchoPayloadCost(complete) <= budget ||
            HasRecognitionSummaries(complete) && EchoPayloadCost(complete, RecognitionWindowText(complete)) <= budget)
            return complete;

        var completion = completionTag is { } tag ? remaining.LastOrDefault(e => IsCompletionFor(e, tag)) : null;

        // The first reply covers the full action window for sighting counts. Other
        // events stay in their original shape and can be paged from the same feed.
        // Keep the requested action's completion in the reply even after slicing.
        // Recognition groups summarize individual repeats. Excluding that whole type
        // would hide identity changes in the continuation.
        string[] excludeTypes = sightings.Where(sighting => IsSighting(sighting.Type)).Select(sighting => sighting.Type).ToArray();
        while (true)
        {
            keep /= 2;
            var prefix = remaining[..keep];
            var visible = completion is not null && !prefix.Contains(completion) ? [..prefix, completion] : prefix;
            var result = JsonSerializer.SerializeToElement(new
            {
                page.Cursor, page.OldestCursor, page.Gap, events = visible, sightings, animations,
                continuation = new { since = keep == 0 ? since : prefix[^1].Cursor, through = page.Cursor, excludeTypes },
            }, Json);
            if (keep == 0 || EchoPayloadCost(result) <= budget) return result;
        }
    }
}
