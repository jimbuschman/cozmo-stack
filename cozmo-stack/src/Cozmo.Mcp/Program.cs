using System.Net;
using System.Text;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Conformance;
using Cozmo.Robot;
using Cozmo.Transport;

namespace Cozmo.Mcp;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.InputEncoding = new UTF8Encoding(false);
        Console.OutputEncoding = new UTF8Encoding(false);
        TextWriter protocolOutput = Console.Out;
        // The engine and its dependencies may write diagnostics to Console.Out.
        // MCP reserves stdout for protocol messages; retain its original writer.
        Console.SetOut(Console.Error);
        try
        {
            var flags = new HashSet<string>(["--resources", "--banks", "--robot-ip", "--out", "--needs", "--animation-budget-bytes", "--event-stream-port", "--significant-events"], StringComparer.Ordinal);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            bool validate = false;
            bool channel = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is "--help" or "-h")
                {
                    Console.Error.WriteLine("cozmo-mcp --resources <cozmo_resources> [--banks <SoundBanks>] [--robot-ip <172.31.1.1>] [--out <session-directory>] [--needs full|real] [--animation-budget-bytes <1..30000>] [--event-stream-port <port>] [--significant-events <type,type,...>] [--channel] [--validate]");
                    return 0;
                }
                if (args[i] == "--channel") { channel = true; continue; }
                if (args[i] == "--validate") { validate = true; continue; }
                string flag = args[i];
                if (!flags.Contains(flag) || i + 1 == args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Unknown option or missing value: {flag}. Use --help.");
                if (!values.TryAdd(flag, args[++i])) throw new ArgumentException($"Duplicate option: {flag}.");
            }
            string resources = EngineStartupResources.OptionsForRoot(values.GetValueOrDefault("--resources")).ResourcesPath!;
            string? banks = values.GetValueOrDefault("--banks");
            if (banks is not null && !Directory.Exists(banks)) throw new ArgumentException("The --banks directory does not exist.");
            var ip = IPAddress.Parse(values.GetValueOrDefault("--robot-ip") ?? "172.31.1.1");
            string needsMode=values.GetValueOrDefault("--needs")??"full";
            if(needsMode is not ("full" or "real"))throw new ArgumentException("--needs requires full or real.");
            int animationBudget=values.TryGetValue("--animation-budget-bytes",out var budgetValue)
                ? HardwareEngineOptions.ParseAnimationBudget(budgetValue) : new CozmoEngineOptions().AnimationStreamBudgetBytes;
            string directory = values.GetValueOrDefault("--out") ?? Path.Combine(FindRepository(), "re-analysis", "acceptance", "hardware",
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-LLM-SESSION");
            int? streamPort = null;
            if (values.TryGetValue("--event-stream-port", out var portValue))
            {
                if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
                    throw new ArgumentException("--event-stream-port requires an integer from 1 to 65535.");
                streamPort = port;
            }
            string[]? significantTypes = values.GetValueOrDefault("--significant-events")?.Split(',', StringSplitOptions.TrimEntries);
            if (significantTypes?.Any(string.IsNullOrWhiteSpace) == true)
                throw new ArgumentException("--significant-events requires nonempty comma-separated event types.");
            if (validate)
            {
                Console.Error.WriteLine($"cozmo-mcp configuration valid. Resources: {resources}. Robot: {ip}. Needs: {needsMode}. Animation budget: {animationBudget} bytes. Evidence: {Path.GetFullPath(directory)}. No connection requested.");
                return 0;
            }

            using var cancel = new CancellationTokenSource();
            ConsoleCancelEventHandler interrupt = (_, e) => { e.Cancel = true; cancel.Cancel(); };
            Console.CancelKeyPress += interrupt;
            try
            {
                if (Environment.GetEnvironmentVariable(SessionProcessSupervisor.WorkerMarker) != "1")
                    return await SessionProcessSupervisor.RunAsync(directory, args,
                        SessionProcessSupervisor.WorkerStart(directory, args), Console.OpenStandardInput(),
                        Console.OpenStandardOutput(), Console.OpenStandardError(), cancel.Token);

                using var evidence = new SessionEvidence(directory, args);
                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                    SessionProcessSupervisor.ReportWorkerFault(evidence.DirectoryPath, "unhandled-thread-exception", e.ExceptionObject);
                TaskScheduler.UnobservedTaskException += (_, e) =>
                    SessionProcessSupervisor.ReportWorkerFault(evidence.DirectoryPath, "unobserved-task-exception", e.Exception);
                using var resolution = new HighResolutionTimer();
                void Log(string text) { Console.Error.WriteLine(text); evidence.Log(text); }
                Log($"cozmo-mcp session evidence: {evidence.DirectoryPath}");
                var narrationClock=System.Diagnostics.Stopwatch.StartNew();
                using var narration=new LiveNarration(Console.Error.WriteLine,()=>narrationClock.Elapsed.TotalMilliseconds,
                    data=>{data["reportedAtMs"]=data["atMs"]?.DeepClone();evidence.Record("live-narration",JsonSerializer.SerializeToElement(data));});
                // Same app inputs and native readiness/connect gate as m15-cube-hw.
                var options = HardwareEngineOptions.CreateWithAnimationBudget(resources, log: Log, speechSoundBankDirectory: banks,
                    beforeStart:engine=>{evidence.Attach(engine);narration.AttachEngine(engine);}, keepNeedsFull: needsMode=="full", animationStreamBudgetBytes: animationBudget);
                using var robot = CozmoRobot.Create(engineOptions: options);
                narration.AttachRobot(robot);
                robot.Transport.FrameTrace += evidence.Frame;
                robot.HandlerFaulted += error => { Log(error.ToString()); evidence.Record("handler-fault", JsonSerializer.SerializeToElement(new { exception = error.ToString() })); };
                using var bridge = new CozmoBridge(robot, significantEventTypes: significantTypes);
                await using var localEvents = streamPort is { } listenPort
                    ? new BridgeEventStream(bridge, listenPort, cancel.Token) : null;
                if (localEvents is not null) Log($"Local events at 127.0.0.1:{localEvents.Port}");
                // Serve initialization immediately while the real engine loads its resources.
                // The bridge exposes native readiness and admission, including not-ready results.
                var startup = Task.Run(() =>
                {
                    try
                    {
                        var gate = HarnessStartup.ConnectAfterReadiness(robot, () => robot.ConnectToRobot(ip), () => cancel.IsCancellationRequested);
                        evidence.Record("startup-gate", JsonSerializer.SerializeToElement(gate.ToJson()));
                        Log("Startup: " + gate.Describe());
                    }
                    catch (Exception error) { Log("Startup: " + error); evidence.Record("startup-error", JsonSerializer.SerializeToElement(new { error = error.ToString() })); }
                });
                try
                {
                    await new CozmoMcpServer(bridge, Log, evidence.Record, channel).RunAsync(Console.In, protocolOutput, cancel.Token);
                    evidence.Finish("closed", bridge.GetState(), exitCode: 0);
                }
                catch (Exception error)
                {
                    evidence.Finish("error", error: error.ToString(), exitCode: 1);
                    throw;
                }
                finally
                {
                    cancel.Cancel();
                    await startup;
                }
                return 0;
            }
            finally { Console.CancelKeyPress -= interrupt; }
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static string FindRepository()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "re-analysis", "SCOPE.md"))) return directory.FullName;
        throw new ArgumentException("Run from the repository or supply --out <session-directory>.");
    }
}
