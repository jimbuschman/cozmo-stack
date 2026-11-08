using System.Text;
using System.Text.Json;

namespace Cozmo.Robot.Animation;

/// <summary>SayText intent traits loaded from the engine's sayTextintentConfig.json.</summary>
internal sealed class SpeechMetadata
{
    private static readonly HashSet<string> Intents = new(StringComparer.Ordinal)
    {
        "Unprocessed", "Text", "Cozmo_Says", "Name_Normal", "Name_FirstIntroduction_1", "Name_FirstIntroduction_2"
    };
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private sealed record Trait(uint MinLength, uint MaxLength, float Min, float Max, float Step);
    private sealed record Entry(byte Style, Trait[] Duration, Trait[] Pitch);
    internal readonly record struct Selection(byte Style, float Duration, float Pitch);

    internal static SpeechMetadata Load(string resources)
    {
        string path = Path.Combine(resources, "config", "engine", "sayTextintentConfig.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var metadata = new SpeechMetadata();
        foreach (var item in doc.RootElement.EnumerateObject())
        {
            if (!Intents.Contains(item.Name) || metadata._entries.ContainsKey(item.Name)) continue;
            var value = item.Value;
            string styleName = value.TryGetProperty("style", out var style) ? style.GetString() ?? "" : "";
            byte styleId = styleName switch
            {
                "Unprocessed" => 0,
                "CozmoProcessing_Name" => 1,
                "CozmoProcessing_Name_Question" => 2,
                _ => 3,
            };
            metadata._entries.Add(item.Name, new Entry(styleId, ReadTraits(value, "durationTraits"), ReadTraits(value, "pitchTraits")));
        }
        return metadata;
    }

    private static Trait[] ReadTraits(JsonElement entry, string name)
    {
        if (!entry.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array) return Array.Empty<Trait>();
        float Float(JsonElement e, string key, float fallback) => e.TryGetProperty(key, out var v) ? v.GetSingle() : fallback;
        uint UInt(JsonElement e, string key, uint fallback) => e.TryGetProperty(key, out var v) ? v.GetUInt32() : fallback;
        return array.EnumerateArray().Select(t => new Trait(UInt(t, "textLengthMin", 0), UInt(t, "textLengthMax", uint.MaxValue),
            Float(t, "rangeMin", BitConverter.UInt32BitsToSingle(0x00800000)), Float(t, "rangeMax", float.MaxValue), Float(t, "stepSize", 0))).ToArray();
    }

    internal bool TrySelect(string intent, string text, EngineRandom random, out Selection selection)
    {
        selection = default;
        if (!_entries.TryGetValue(intent, out var entry)) return false;
        if (entry.Duration.Length == 0 || entry.Pitch.Length == 0)
            throw new InvalidDataException("M1-034 M14-006: SayText intent has no trait; skipped invalid trait access");
        uint length = (uint)Encoding.UTF8.GetByteCount(text);
        float Pick(Trait[] traits)
        {
            var trait = traits.FirstOrDefault(t => t.MinLength <= length && length <= t.MaxLength) ?? traits[0];
            if (!(trait.Step > 0.00001f)) return trait.Min;
            int count = (int)((trait.Max - trait.Min) / trait.Step + 1f);
            return trait.Min + trait.Step * random.RandInt(count);
        }
        selection = new Selection(entry.Style, Pick(entry.Duration), Pick(entry.Pitch));
        return true;
    }
}
