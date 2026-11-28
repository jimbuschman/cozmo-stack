using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Cozmo.Bridge;
using Cozmo.Conformance;
using Cozmo.Robot;

namespace Cozmo.Mcp;

// Our C2 policy sits above the native receive watchdog (0x00836080..98).
// Each attempt goes through the same startup gate as the first connection.
internal sealed class C2LinkRecovery : IAsyncDisposable
{
    private abstract record Signal;
    private sealed record Lost(RobotDisconnectReason Reason) : Signal;
    private sealed record Answer(RobotConnectionResult Result) : Signal;
    private readonly Channel<Signal> signals = Channel.CreateUnbounded<Signal>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly CozmoRobot robot;
    private readonly CozmoBridge bridge;
    private readonly IPAddress address;
    private readonly Action<string> log;
    private readonly Action<string, JsonElement> record;
    private readonly CancellationTokenSource stop;
    private readonly Task worker;
    private readonly Func<CancellationToken, HarnessStartup.Gate> connectGate;
    private readonly TimeSpan retryInterval, lostInterval;

    internal C2LinkRecovery(CozmoRobot robot, CozmoBridge bridge, IPAddress address,
        Action<string> log, Action<string, JsonElement> record, CancellationToken cancellation,
        Func<CancellationToken, HarnessStartup.Gate>? connectGate = null,
        TimeSpan? retryInterval = null, TimeSpan? lostInterval = null)
    {
        this.robot = robot;
        this.bridge = bridge;
        this.address = address;
        this.log = log;
        this.record = record;
        this.connectGate = connectGate ?? (ct => HarnessStartup.ConnectAfterReadiness(robot,
            () => robot.ConnectToRobot(address), () => ct.IsCancellationRequested));
        this.retryInterval = retryInterval ?? TimeSpan.FromSeconds(5);
        this.lostInterval = lostInterval ?? TimeSpan.FromMinutes(1);
        stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        robot.Engine.RobotDisconnected += OnLost;
        robot.Engine.ConnectionResponse += OnAnswer;
        worker = Run(stop.Token);
    }

    private void OnLost(RobotDisconnectedMessage message) => signals.Writer.TryWrite(new Lost(message.Reason));
    private void OnAnswer(RobotConnectionResponse response) => signals.Writer.TryWrite(new Answer(response.Result));

    private void Event(string type, string reason)
    {
        bridge.ReportLinkRecovery(type, reason);
        log($"{CozmoC2Settings.LinkReconnectName}: {type}, {reason}");
        record(type, JsonSerializer.SerializeToElement(new { reason, lostState = new[] { "actions", "map origin", "face IDs" } }));
    }

    private async Task Run(CancellationToken token)
    {
        bool recovering = false, awaitingAnswer = false;
        // Host retry/report policy; the native watchdog, keepalive and 0x004ED020
        // connection request remain unchanged. Never start a second handshake
        // while the first is outstanding.
        var clock = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan nextAttempt = TimeSpan.Zero, nextNotice = TimeSpan.Zero;
        try
        {
            while (!token.IsCancellationRequested)
            {
                while (signals.Reader.TryRead(out var signal))
                {
                    switch (signal)
                    {
                        case Lost { Reason: RobotDisconnectReason.WifiTimeout } when !recovering:
                            recovering = true;
                            awaitingAnswer = false;
                            nextAttempt = clock.Elapsed;
                            nextNotice = clock.Elapsed + lostInterval;
                            Event("link-lost", "WifiTimeout");
                            break;
                        case Lost { Reason: RobotDisconnectReason.WifiTimeout }:
                            awaitingAnswer = false;
                            nextAttempt = clock.Elapsed + retryInterval;
                            break;
                        case Lost:
                            recovering = awaitingAnswer = false;
                            break;
                        case Answer { Result: RobotConnectionResult.Success } when recovering:
                            recovering = awaitingAnswer = false;
                            Event("reconnected", "native connection handshake succeeded");
                            break;
                        case Answer failure when recovering:
                            awaitingAnswer = false;
                            nextAttempt = clock.Elapsed + retryInterval;
                            log($"{CozmoC2Settings.LinkReconnectName}: handshake returned {failure.Result}, retrying");
                            break;
                    }
                }
                if (recovering && clock.Elapsed >= nextNotice)
                {
                    Event("link-still-lost", "native connection not yet restored");
                    nextNotice = clock.Elapsed + lostInterval;
                }
                if (recovering && !awaitingAnswer && clock.Elapsed >= nextAttempt)
                {
                    token.ThrowIfCancellationRequested();
                    log($"{CozmoC2Settings.LinkReconnectName}: reconnect-attempt");
                    record("reconnect-attempt", JsonSerializer.SerializeToElement(new { reason = "WifiTimeout" }));
                    var gate = await Task.Run(() => connectGate(token), token).ConfigureAwait(false);
                    log($"{CozmoC2Settings.LinkReconnectName}: {gate.Describe()}");
                    record("reconnect-gate", JsonSerializer.SerializeToElement(gate.ToJson()));
                    awaitingAnswer = gate.Ready;
                    nextAttempt = clock.Elapsed + retryInterval;
                    continue;
                }
                if (!recovering)
                {
                    if (!await signals.Reader.WaitToReadAsync(token).ConfigureAwait(false)) break;
                    continue;
                }
                var deadline = awaitingAnswer || nextNotice < nextAttempt ? nextNotice : nextAttempt;
                var wait = deadline - clock.Elapsed;
                if (wait <= TimeSpan.Zero) continue;
                using var timer = CancellationTokenSource.CreateLinkedTokenSource(token);
                timer.CancelAfter(wait);
                try { if (!await signals.Reader.WaitToReadAsync(timer.Token).ConfigureAwait(false)) break; }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        robot.Engine.RobotDisconnected -= OnLost;
        robot.Engine.ConnectionResponse -= OnAnswer;
        stop.Cancel();
        signals.Writer.TryComplete();
        try { await worker.ConfigureAwait(false); }
        finally { stop.Dispose(); }
    }
}
