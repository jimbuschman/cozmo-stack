using System.Text.Json;
namespace Cozmo.Robot;

// fidelity: M3-053
/// <summary>libDAS sink (a) only. No remote queue, globals or game-file sink.</summary>
public sealed record DisplayedEngineLog(int Priority, string Tag, string Line);

// fidelity: M3-052, M3-053
internal sealed class DasLogDisplay
{
    internal int LocalLevel { get; private set; } = 5; // .data before a successful configure
    internal int RemoteLevel { get; private set; } = 2;
    internal int GameLevel { get; private set; } = 1;
    internal string Url { get; private set; } = "";
    internal int FlushInterval { get; private set; } = 5;
    internal int MaxLogLength { get; private set; } = 0x19000;
    internal int MaxLogFiles { get; private set; } = 0x190;
    private readonly Dictionary<string, int> _overrides = new(StringComparer.Ordinal);

    internal static string ShippedConfiguration()
    {
        using var stream = typeof(DasLogDisplay).Assembly.GetManifestResourceStream("Cozmo.Robot.DASConfig.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal void Configure(string json, Action<DisplayedEngineLog> diagnostic)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }); }
        catch (JsonException)
        {
            // Section 13.5 gives the native format, priority/tag and data defaults, but not JsonCpp's reader-error bytes.
            diagnostic(new(3, "daslib", "MISSING: M3-053 | DASConfigure 0x6694..0x6C28: Failed to parse configuration: %s requires the unread JsonCpp formatted error argument"));
            return;
        }
        using (doc)
        {
            var root = doc.RootElement;
            JsonElement cfg = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("dasConfig", out var config) ? config : default;
            string Str(string name, string fallback) => cfg.ValueKind == JsonValueKind.Object && cfg.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : fallback;
            int Int(string name, int fallback) => cfg.ValueKind == JsonValueKind.Object && cfg.TryGetProperty(name, out var value) && value.TryGetInt32(out int number) ? number : fallback;
            LocalLevel = Level(Str("localMinLogLevel", "info"));
            RemoteLevel = Level(Str("remoteMinLogLevel", "event"));
            GameLevel = Level(Str("gameMinLogLevel", "info"));
            Url = Str("url", ""); FlushInterval = Int("flushInterval", 5);
            MaxLogLength = Int("maxLogLength", 0x19000); MaxLogFiles = Int("maxLogFiles", 0x190);
            _overrides.Clear();
            if (cfg.ValueKind == JsonValueKind.Object && cfg.TryGetProperty("localMinLogLevelOverrides", out var overrides) && overrides.ValueKind == JsonValueKind.Object)
                foreach (var property in overrides.EnumerateObject())
                    if (property.Value.ValueKind == JsonValueKind.String) _overrides[property.Name] = Level(property.Value.GetString()!);
        }
    }

    private static int Level(string name) => name switch { "debug" => 0, "info" => 1, "event" => 2, "warn" => 3, "error" => 4, _ => 5 };

    internal int GetLevel(string name)
    {
        // Section 13.5: whole dot segments; the closest matching ancestor overrides the root.
        int level = LocalLevel;
        int end = 0;
        while (end < name.Length)
        {
            int dot = name.IndexOf('.', end);
            int next = dot < 0 ? name.Length : dot;
            if (_overrides.TryGetValue(name[..next], out int value)) level = value;
            if (dot < 0) break;
            end = dot + 1;
        }
        return level;
    }

    // fidelity: M3-053, M3-054
    internal DisplayedEngineLog? Render(EngineLogEntry entry, bool verbose, Action<string> missing)
    {
        string name = entry.Name ?? "";
        if (name.Length == 0)
        {
            missing("MISSING: M3-053 | libDAS core 0x742E..0x75EE: the adopted null-name diagnostic is abbreviated as DAS ERROR: EVENT WITH A NULL OR BLANK NAME PASSED ...; full literal unread");
            name = "noname";
        }
        if (!verbose && (int)entry.Level < GetLevel(name)) return null;
        // Phone ctype whitespace, a system-library property: ASCII whitespace for these printf texts.
        string text = entry.Text.TrimEnd(' ', '\t', '\n', '\r', '\v', '\f');
        if (verbose && entry.Channel is { } channel) text = "[" + channel + "] " + text;
        int priority = entry.Level switch { EngineLogLevel.Debug => 3, EngineLogLevel.Info or EngineLogLevel.Event => 4, EngineLogLevel.Warning => 5, _ => 6 };
        return new(priority, "das", name + " : " + text + " ");
    }
}
