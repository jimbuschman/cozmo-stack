using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Cozmo.Conformance;

internal sealed record HarnessRevision(string? Commit, string? HeadCommit, int? BehindOriginMain, int? BuildBehindOriginMain)
{
    private const string MainRef = "refs/remotes/origin/main";

    internal static HarnessRevision Check(string root, TextWriter output)
    {
        // The SDK appends SourceRevisionId at build time. Reading HEAD at run time cannot identify a copied DLL.
        string? version = typeof(HarnessRevision).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string? build = Revision(version?.Split('+').LastOrDefault());
        output.WriteLine($"Build commit {build ?? "(unavailable)"}");
        string? head = Revision(Git(root, "rev-parse", "HEAD").Output);
        var fetch = Git(root, "fetch", "--no-tags", "origin", $"+refs/heads/main:{MainRef}");
        if (!fetch.Ok)
        {
            output.WriteLine($"Could not fetch origin/main. Continuing without a freshness check. {fetch.Error}");
            return new(build, head, null, null);
        }

        int? CountBehind(string? revision)
        {
            if (revision is null) return null;
            var count = Git(root, "rev-list", "--count", $"{revision}..{MainRef}");
            return count.Ok && int.TryParse(count.Output, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? parsed : null;
        }

        int? headBehind = CountBehind(head);
        int? buildBehind = head == build ? headBehind : CountBehind(build);
        if (headBehind is null || buildBehind is null)
            output.WriteLine("Could not compare all revisions with origin/main. Unknown counts are recorded as null.");
        if (headBehind > 0 || buildBehind > 0)
        {
            if (headBehind > 0) output.WriteLine($"WARNING: HEAD is {headBehind} commit(s) behind origin/main.");
            if (buildBehind > 0 && build != head)
                output.WriteLine($"WARNING: This binary was built {buildBehind} commit(s) behind origin/main.");
            output.WriteLine("Run git pull and rebuild before using the harness for current robot results.");
        }
        return new(build, head, headBehind, buildBehind);
    }

    internal bool Confirm(bool yes, TextReader input, TextWriter output)
    {
        if (yes || (BehindOriginMain is not > 0 && BuildBehindOriginMain is not > 0)) return true;
        output.WriteLine("Press Enter to continue with this older revision, or Ctrl+C to stop.");
        return input.ReadLine() == "";
    }

    private static string? Revision(string? text) =>
        (text?.Length is 40 or 64) && text.All(Uri.IsHexDigit) ? text : null;

    private static (bool Ok, string? Output, string? Error) Git(string root, params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true,
        };
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        info.Environment["GCM_INTERACTIVE"] = "Never";
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        try
        {
            using var process = Process.Start(info);
            if (process is null) return (false, null, "Git did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15_000))
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                return (false, null, "Git did not finish within 15 seconds.");
            }
            return (process.ExitCode == 0, stdout.GetAwaiter().GetResult().Trim(), stderr.GetAwaiter().GetResult().Trim());
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return (false, null, error.Message);
        }
    }
}
