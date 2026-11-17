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
            var flags = new HashSet<string>(["--resources", "--banks", "--robot-ip", "--out"], StringComparer.Ordinal);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            bool validate = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is "--help" or "-h")
                {
                    Console.Error.WriteLine("cozmo-mcp --resources <cozmo_resources> [--banks <SoundBanks>] [--robot-ip <172.31.1.1>] [--out <session-directory>] [--validate]");
                    return 0;
                }
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
            string directory = values.GetValueOrDefault("--out") ?? Path.Combine(FindRepository(), "re-analysis", "acceptance", "hardware",
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-LLM-SESSION");
            if (validate)
            {
                Console.Error.WriteLine($"cozmo-mcp configuration valid. Resources: {resources}. Robot: {ip}. Evidence: {Path.GetFullPath(directory)}. No connection requested.");
                return 0;
            }

            using var cancel = new CancellationTokenSource();
            ConsoleCancelEventHandler interrupt = (_, e) => { e.Cancel = true; cancel.Cancel(); };
            Console.CancelKeyPress += interrupt;
            try
            {
                using var evidence = new SessionEvidence(directory, args);
                using var resolution = new HighResolutionTimer();
                void Log(string text) { Console.Error.WriteLine(text); evidence.Log(text); }
                Log($"cozmo-mcp session evidence: {evidence.DirectoryPath}");
                // Same app inputs and native readiness/connect gate as m15-cube-hw.
                var options = HardwareEngineOptions.Create(resources, log: Log, speechSoundBankDirectory: banks, beforeStart: evidence.Attach);
                using var robot = CozmoRobot.Create(engineOptions: options);
                robot.Transport.FrameTrace += evidence.Frame;
                robot.HandlerFaulted += error => { Log(error.ToString()); evidence.Record("handler-fault", JsonSerializer.SerializeToElement(new { exception = error.ToString() })); };
                using var bridge = new CozmoBridge(robot);
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
                    await new CozmoMcpServer(bridge, Log, evidence.Record).RunAsync(Console.In, protocolOutput, cancel.Token);
                    evidence.Finish("closed", bridge.GetState());
                }
                catch (Exception error)
                {
                    evidence.Finish("error", error: error.ToString());
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
