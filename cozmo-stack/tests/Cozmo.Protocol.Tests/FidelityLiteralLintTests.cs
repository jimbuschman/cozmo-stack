using System.Text.RegularExpressions;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The float-literal lint (re-analysis/jobs/CHECKLIST.md, section 4). The 2026-09-29 audit found engine constants written
/// as rounded decimals a few ULPs off the engine's own floats: for example -0.436332f for 0xBEDF66F3, and 0.712121f for
/// 0x3F364D93. Engine constants belong in code as their bit patterns.
///
/// This lint flags every decimal literal with six to eight significant digits (precise-looking but possibly rounded;
/// nine digits round-trip any float exactly, so full transcriptions pass) in code that carries a
/// <c>// fidelity:</c> tag, comments and strings excluded. The ones that existed when the lint was added are listed in
/// Fixtures/float_literal_baseline.txt. Only a new one fails: fix it (use the bit pattern), don't add it to the
/// baseline. A baseline entry that no longer appears can be removed.
/// </summary>
public class FidelityLiteralLintTests
{
    private static readonly Regex Literal = new(@"(?<![\w.])(\d+\.\d+(?:[eE][-+]?\d+)?|\d+[eE][-+]?\d+)[fFdDmM]?(?![\w.])",
        RegexOptions.Compiled);

    internal static IEnumerable<(string File, string Literal)> Scan(string srcRoot)
    {
        foreach (var path in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            var text = File.ReadAllText(path);
            if (!text.Contains("// fidelity:")) continue;
            var rel = Path.GetRelativePath(srcRoot, path).Replace('\\', '/');
            foreach (var raw in text.Split('\n'))
            {
                string line = StripStringsAndComments(raw);
                foreach (Match m in Literal.Matches(line))
                    if (SignificantDigits(m.Groups[1].Value) is >= 6 and <= 8) yield return (rel, m.Groups[1].Value);
            }
        }
    }

    private static string StripStringsAndComments(string line)
    {
        var sb = new System.Text.StringBuilder();
        bool inString = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (!inString && c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;
            if (c == '"') { inString = !inString; continue; }
            if (inString) { if (c == '\\') i++; continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static int SignificantDigits(string literal)
    {
        string mantissa = literal.Split('e', 'E')[0].Replace(".", "").TrimStart('0').TrimEnd('0');
        return mantissa.Length;
    }

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "src", "Cozmo.Robot"))) d = d.Parent;
        return Path.Combine(d!.FullName, "src");
    }

    [Fact]
    public void NoNewLongDecimalLiteralsInFidelityTaggedCode()
    {
        var baselinePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "float_literal_baseline.txt");
        if (Environment.GetEnvironmentVariable("COZMO_WRITE_LITERAL_BASELINE") is { } target)
        {
            // One-off: write the baseline (used once, when the lint was added). Never use it to admit a new literal.
            var lines = new[]
            {
                "# Long decimal literals in fidelity-tagged code when the lint was added (2026-09-29).",
                "# Each is a candidate engine constant to rewrite as its bit pattern. Remove a line when it is fixed; never add one.",
            }.Concat(Scan(Root()).Select(x => $"{x.File} {x.Literal}").Distinct().OrderBy(s => s, StringComparer.Ordinal));
            File.WriteAllLines(target, lines);
            return;
        }
        var baseline = new HashSet<string>(File.ReadAllLines(baselinePath).Where(l => l.Length > 0 && l[0] != '#'));
        var found = Scan(Root()).Select(x => $"{x.File} {x.Literal}").Distinct().ToList();
        var fresh = found.Where(f => !baseline.Contains(f)).ToList();
        Assert.True(fresh.Count == 0,
            "New long decimal literals in fidelity-tagged code (write the engine's bit pattern instead; see " +
            "re-analysis/jobs/CHECKLIST.md section 4):\n" + string.Join("\n", fresh));
    }
}
