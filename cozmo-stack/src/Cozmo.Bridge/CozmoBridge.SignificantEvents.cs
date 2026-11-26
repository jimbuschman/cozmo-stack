using System.Runtime.CompilerServices;
using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;

namespace Cozmo.Bridge;

public sealed record BridgeEvent(long Cursor, string Type, DateTimeOffset TimestampUtc,
    uint? RobotTimestamp, JsonElement Payload, bool Significant);

public sealed record BridgeEventPage(long Cursor, long OldestCursor, bool Gap, IReadOnlyList<BridgeEvent> Events);

public sealed partial class CozmoBridge
{
    private static readonly HashSet<string> DefaultSignificantTypes = new(StringComparer.Ordinal)
    {
        "face-recognised", "face-seen", "cube-seen", "charger-seen", "object-seen", "object-pose-lost",
        "cube-tapped", "cube-moved", "off-treads-changed", "cliff-detected", "unexpected-movement",
        "reaction-changed", "hiccups-started", "action-completed", "going-to-sleep", "sleep-cancelled", "robot-disconnected",
        "low-battery", "on-charger", "off-charger",
    };

    private readonly object eventCaller = new();
    private readonly TimeProvider timeProvider;
    private readonly HashSet<string> significantTypes;
    private readonly HashSet<int> locatedFaces = [];
    private readonly HashSet<uint> locatedObjects = [];
    private readonly Dictionary<int, DateTimeOffset> recognisedAt = [];
    private DateTimeOffset recognitionSweep;
    private bool batteryLow;
    private bool onCharger;

    // Called with the feed lock so the sighting and its recognition stay adjacent.
    private bool FirstRecognitionThisMinute(int id)
    {
        var now = timeProvider.GetUtcNow();
        var minute = TimeSpan.FromMinutes(1);
        if (now - recognitionSweep >= minute)
        {
            foreach (var expired in recognisedAt.Where(pair => now - pair.Value >= minute).Select(pair => pair.Key).ToArray())
                recognisedAt.Remove(expired);
            recognitionSweep = now;
        }
        if (recognisedAt.TryGetValue(id, out var previous) && now - previous < minute) return false;
        recognisedAt[id] = now;
        return true;
    }

    private void CliffDetected(CliffReport report) =>
        Add(report.Sensors == CliffSensors.None ? "cliff-cleared" : "cliff-detected", report, report.Timestamp);
    private void UnexpectedMovement(UnexpectedMovementReport report) => Add("unexpected-movement", report, report.Timestamp);

    private void PowerState(RobotState state)
    {
        // Same off-charger threshold used by native charging lights (M4-017, 0x00631B94..0x00631BD8).
        bool contacts = state.Has(RobotStatusFlag.IsOnCharger);
        bool low = !contacts && float.IsFinite(state.BatteryVoltage) && state.BatteryVoltage < 3.5f;
        lock (eventGate)
        {
            if (contacts != onCharger)
                Add(contacts ? "on-charger" : "off-charger", new { isOnCharger = contacts }, state.Timestamp);
            onCharger = contacts;
            if (low && !batteryLow) Add("low-battery", new { batteryVolts = state.BatteryVoltage }, state.Timestamp);
            batteryLow = low;
        }
    }

    /// <summary>
    /// Follows retained and live events. A null cursor starts at the live edge. Use each page cursor to
    /// resume. Gap indicates that the ring has evicted part of the unscanned history.
    /// </summary>
    public IAsyncEnumerable<BridgeEventPage> StreamEventsAsync(long? since = null,
        IReadOnlyCollection<string>? types = null, bool significantOnly = false,
        CancellationToken token = default) =>
        StreamEventViewAsync(since, types, significantOnly, EventView.Full, token);

    internal IAsyncEnumerable<BridgeEventPage> StreamPushEventsAsync(long since, CancellationToken token) =>
        StreamEventViewAsync(since, null, true, EventView.Push, token);

    private async IAsyncEnumerable<BridgeEventPage> StreamEventViewAsync(long? since,
        IReadOnlyCollection<string>? types, bool significantOnly, EventView view,
        [EnumeratorCancellation] CancellationToken token)
    {
        long cursor = since ?? CurrentEventCursor();
        ArgumentOutOfRangeException.ThrowIfNegative(cursor);
        var include = types?.ToHashSet(StringComparer.Ordinal);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var page = await WaitEventPageAsync(cursor, 30000, 64, include, null, significantOnly, token, view).ConfigureAwait(false);
            cursor = page.Cursor;
            if (page.Gap || page.Events.Count != 0) yield return page;
        }
    }
}
