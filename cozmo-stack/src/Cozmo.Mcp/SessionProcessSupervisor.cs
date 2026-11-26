using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cozmo.Mcp;

// Host lifecycle only: the original engine never owned this stand-alone MCP process.
// The worker cannot record a native/CLR hard exit itself; the parent sees the OS exit code.
internal static class SessionProcessSupervisor
{
    internal const string WorkerMarker = "COZMO_MCP_SESSION_WORKER";
    internal const string StderrName = "process.stderr.log";
    internal const string FaultsName = "process-faults.log";

    internal static ProcessStartInfo WorkerStart(string directory, string[] arguments)
    {
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Current process executable is unavailable");
        var start = new ProcessStartInfo(executable);
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        if (!arguments.Contains("--out", StringComparer.Ordinal))
        {
            start.ArgumentList.Add("--out");
            start.ArgumentList.Add(Path.GetFullPath(directory));
        }
        start.Environment[WorkerMarker] = "1";
        return start;
    }

    internal static async Task<int> RunAsync(string directory, string[] arguments, ProcessStartInfo start,
        Stream input, Stream protocol, Stream diagnostics, CancellationToken cancellation = default)
    {
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        string stderrPath = Path.Combine(directory, StderrName);
        // Same create-new rule as the worker's evidence journals: don't overwrite a previous session.
        if (File.Exists(Path.Combine(directory, "result.json")))
            throw new IOException("The output session directory already contains result.json.");
        using var stderr = new FileStream(stderrPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, true);
        var startedUtc = DateTimeOffset.UtcNow;
        start.UseShellExecute = false;
        start.RedirectStandardInput = start.RedirectStandardOutput = start.RedirectStandardError = true;
        start.CreateNoWindow = true;
        using var child = new Process { StartInfo = start };
        int exitCode = 1;
        string? supervisorError = null;
        bool started = false;
        try
        {
            if (!child.Start()) throw new InvalidOperationException("Could not start the MCP session worker");
            started = true;
            using var stopInput = new CancellationTokenSource();
            void CloseInput()
            {
                try { child.StandardInput.Close(); }
                catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException) { }
            }
            using var cancel = cancellation.Register(CloseInput);
            var feeding = Task.Run(async () =>
            {
                try { await input.CopyToAsync(child.StandardInput.BaseStream, stopInput.Token).ConfigureAwait(false); }
                catch (Exception error) when (error is IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException) { }
                finally { CloseInput(); }
            });
            var output = ForwardAsync(child.StandardOutput.BaseStream, protocol, null, CloseInput);
            var errors = ForwardAsync(child.StandardError.BaseStream, diagnostics, stderr, null);
            await child.WaitForExitAsync().ConfigureAwait(false);
            exitCode = child.ExitCode;
            stopInput.Cancel(); // a client may retain stdin after a hard worker exit; never wait on that pipe.
            CloseInput();
            if (feeding.IsCompleted) await feeding.ConfigureAwait(false);
            await Task.WhenAll(output, errors).ConfigureAwait(false);
            supervisorError = errors.Result;
            if (output.Result is { } outputError) supervisorError = supervisorError is null ? outputError : supervisorError + "; " + outputError;
        }
        catch (Exception error)
        {
            supervisorError = error.ToString();
            if (started)
            {
                if (!child.HasExited)
                {
                    try { child.StandardInput.Close(); }
                    catch (Exception closeError) when (closeError is IOException or InvalidOperationException) { }
                    if (!child.WaitForExit(3000)) child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync().ConfigureAwait(false);
                }
                exitCode = child.ExitCode;
            }
            byte[] text = Encoding.UTF8.GetBytes("MCP supervisor: " + supervisorError + Environment.NewLine);
            await stderr.WriteAsync(text).ConfigureAwait(false);
            try { await diagnostics.WriteAsync(text).ConfigureAwait(false); await diagnostics.FlushAsync().ConfigureAwait(false); }
            catch (IOException) { }
        }
        finally { await stderr.FlushAsync().ConfigureAwait(false); }
        stderr.Dispose();
        FinalizeResult(directory, arguments, startedUtc, started ? child.Id : null, exitCode, supervisorError);
        return exitCode;
    }

    private static async Task<string?> ForwardAsync(Stream source, Stream destination, Stream? journal, Action? disconnected)
    {
        var buffer = new byte[8192];
        bool forward = true;
        string? failure = null;
        while (true)
        {
            int count;
            try { count = await source.ReadAsync(buffer).ConfigureAwait(false); }
            catch (Exception error) when (error is IOException or ObjectDisposedException)
            {
                disconnected?.Invoke();
                return "Could not read worker stream: " + error.Message;
            }
            if (count == 0) break;
            if (journal is not null)
            {
                try
                {
                    await journal.WriteAsync(buffer.AsMemory(0, count)).ConfigureAwait(false);
                    await journal.FlushAsync().ConfigureAwait(false);
                }
                catch (IOException error) { failure = "Could not write worker stderr: " + error.Message; journal = null; }
            }
            if (!forward) continue;
            try
            {
                await destination.WriteAsync(buffer.AsMemory(0, count)).ConfigureAwait(false);
                await destination.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or ObjectDisposedException)
            {
                forward = false;
                disconnected?.Invoke(); // still drain the child to avoid blocking its shutdown.
            }
        }
        return failure;
    }

    private static void FinalizeResult(string directory, string[] arguments, DateTimeOffset startedUtc,
        int? workerPid, int exitCode, string? supervisorError)
    {
        string path = Path.Combine(directory, "result.json");
        JsonObject result = new() { ["testId"] = "LLM-SESSION", ["arguments"] = JsonSerializer.SerializeToNode(arguments), ["startedUtc"] = startedUtc };
        try { if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject childResult) result = childResult; }
        catch (Exception error) when (error is IOException or JsonException) { result["workerResultReadError"] = error.Message; }
        string? status = result["status"]?.GetValue<string>();
        if (exitCode != 0)
        {
            result["status"] = "error";
            result["error"] ??= $"MCP worker exited with code {exitCode} (0x{unchecked((uint)exitCode):X8})";
        }
        else if (status is null or "running") result["status"] = "incomplete";
        result["workerPid"] = workerPid;
        result["exitCode"] = exitCode;
        result["exitCodeHex"] = $"0x{unchecked((uint)exitCode):X8}";
        result["processEndedUtc"] = DateTimeOffset.UtcNow;
        result["endedUtc"] ??= DateTimeOffset.UtcNow;
        result["durationMs"] ??= (DateTimeOffset.UtcNow - startedUtc).TotalMilliseconds;
        result["stderrFile"] = StderrName;
        using (var log = File.OpenRead(Path.Combine(directory, StderrName)))
        {
            int length = (int)Math.Min(log.Length, 8192);
            log.Seek(-length, SeekOrigin.End);
            var tail = new byte[length]; log.ReadExactly(tail);
            result["stderrTail"] = Encoding.UTF8.GetString(tail);
            result["stderrTailTruncated"] = log.Length > length;
        }
        if (File.Exists(Path.Combine(directory, FaultsName))) result["faultsFile"] = FaultsName;
        if (supervisorError is not null) result["supervisorError"] = supervisorError;
        if (exitCode != 0 || status != "closed" || !File.Exists(Path.Combine(directory, "messages.jsonl")))
        {
            try
            {
                var (frames, invalid) = SessionEvidence.RecoverMessages(directory);
                result["frames"] = frames;
                result["invalidFrameRows"] = invalid;
            }
            catch (Exception error) { result["messageRecoveryError"] = error.ToString(); }
        }
        SessionEvidence.WriteResult(directory, result);
    }

    // CLR thread-fatal and unobserved task exceptions are separate from handled engine faults.
    // Append directly rather than locking the worker evidence object on a crashing thread.
    internal static void ReportWorkerFault(string directory, string kind, object? error)
    {
        string message = $"{DateTimeOffset.UtcNow:O} {kind}: {error}";
        try { Console.Error.WriteLine(message); }
        catch (IOException) { }
        try { File.AppendAllText(Path.Combine(directory, FaultsName), message + Environment.NewLine); }
        catch (Exception fault) when (fault is IOException or UnauthorizedAccessException) { }
    }
}