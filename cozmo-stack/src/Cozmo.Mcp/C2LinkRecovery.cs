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

    internal C2LinkRecovery(CozmoRobot robot, CozmoBridge bridge, IPAddress address,
        Action<string> log, Action<string, JsonElement> record, CancellationToken cancellation)
    {
        this.robot = robot;
        this.bridge = bridge;
        this.address = address;
        this.log = log;
        this.record = record;
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
        bool recovering = false;
        try
        {
            await foreach (var signal in signals.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                switch (signal)
                {
                    case Lost { Reason: RobotDisconnectReason.WifiTimeout }:
                        if (recovering) continue;
                        recovering = true;
                        Event("link-lost", "WifiTimeout");
                        break;
                    case Lost:
                        recovering = false;
                        continue;
                    case Answer when !recovering:
                        continue;
                    case Answer { Result: RobotConnectionResult.Success }:
                        recovering = false;
                        Event("reconnected", "native connection handshake succeeded");
                        continue;
                    case Answer failure:
                        log($"{CozmoC2Settings.LinkReconnectName}: handshake returned {failure.Result}, retrying");
                        await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                        break;
                }
                var gate = HarnessStartup.ConnectAfterReadiness(robot, () => robot.ConnectToRobot(address),
                    () => token.IsCancellationRequested);
                log($"{CozmoC2Settings.LinkReconnectName}: {gate.Describe()}");
                record("reconnect-gate", JsonSerializer.SerializeToElement(gate.ToJson()));
                if (!gate.Ready) recovering = false;
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
