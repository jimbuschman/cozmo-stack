using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cozmo.Bridge;

internal sealed record Argument(string Name, string Type, string Description, double? Minimum = null,
    double? Maximum = null, JsonElement? Default = null, string[]? Choices = null, int? MaxUtf8Bytes = null, bool AllowEmpty = false)
{
    internal static Argument Number(string name, string description, float? defaultValue = null) =>
        new(name, "number", description, -float.MaxValue, float.MaxValue, DefaultOf(defaultValue));

    internal static Argument Integer(string name, string description, long min, long max, long? defaultValue = null) =>
        new(name, "integer", description, min, max, DefaultOf(defaultValue));

    internal static Argument Boolean(string name, string description, bool? defaultValue = null) =>
        new(name, "boolean", description, Default: DefaultOf(defaultValue));

    internal static Argument Text(string name, string description, int? maxUtf8Bytes = null, string[]? choices = null,
        bool allowEmpty = false) =>
        new(name, "string", description, Choices: choices, MaxUtf8Bytes: maxUtf8Bytes, AllowEmpty: allowEmpty);

    private static JsonElement? DefaultOf<T>(T? value) where T : struct =>
        value.HasValue ? JsonSerializer.SerializeToElement(value.Value) : null;

    internal JsonObject Describe()
    {
        if (Type == "reaction-selection")
            return new JsonObject
            {
                ["description"] = Description,
                ["oneOf"] = new JsonArray(
                    new JsonObject { ["type"] = "string", ["const"] = "all" },
                    new JsonObject
                    {
                        ["type"] = "array", ["minItems"] = 1, ["uniqueItems"] = true,
                        ["items"] = new JsonObject { ["type"] = "string", ["enum"] = JsonSerializer.SerializeToNode(Choices) },
                    }),
            };
        var schema = new JsonObject { ["type"] = Type, ["description"] = Description };
        if (Minimum is { } min) schema["minimum"] = min;
        if (Maximum is { } max) schema["maximum"] = max;
        if (Default is { } fallback) schema["default"] = JsonNode.Parse(fallback.GetRawText());
        if (Choices is not null) schema["enum"] = JsonSerializer.SerializeToNode(Choices);
        if (Type == "string" && !AllowEmpty) schema["minLength"] = 1;
        if (MaxUtf8Bytes is { } length) schema["maxLength"] = length;
        return schema;
    }

    internal static Argument ReactionSelection(string[] choices) => new("triggers", "reaction-selection",
        "Use 'all' or an array of engine reaction trigger names.", Choices: choices);

    internal bool Accepts(JsonElement value) => Type switch
    {
        "number" => value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float number) && float.IsFinite(number)
            && value.TryGetDouble(out double precise) && precise >= Minimum && precise <= Maximum,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out decimal integer)
            && integer == decimal.Truncate(integer) && integer >= (decimal)Minimum!.Value && integer <= (decimal)Maximum!.Value,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "reaction-selection" => value.ValueKind == JsonValueKind.String && value.GetString() == "all" ||
            value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0 &&
            value.GetArrayLength() <= Choices!.Length && value.EnumerateArray().All(item =>
                item.ValueKind == JsonValueKind.String && Choices.Contains(item.GetString(), StringComparer.Ordinal)) &&
            value.EnumerateArray().Select(item => item.GetString()).Distinct(StringComparer.Ordinal).Count() == value.GetArrayLength(),
        "string" => value.ValueKind == JsonValueKind.String && value.GetString() is { } text
            && (AllowEmpty || text.Length > 0)
            && (Choices is null || Array.IndexOf(Choices, text) >= 0)
            && (MaxUtf8Bytes is null || Encoding.UTF8.GetByteCount(text) <= MaxUtf8Bytes),
        _ => false,
    };
}

internal sealed record BridgeCommand(string Name, string Description, Argument[] Parameters,
    Func<CozmoBridge, BridgeArguments, CancellationToken, Task<CommandResult>> Execute)
{
    internal object Describe()
    {
        var properties = new JsonObject();
        foreach (var parameter in Parameters) properties.Add(parameter.Name, parameter.Describe());
        return new
        {
            Name,
            Description,
            ArgumentsSchema = new JsonObject
            {
                ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = JsonSerializer.SerializeToNode(Parameters.Where(p => p.Default is null).Select(p => p.Name)),
                ["additionalProperties"] = false,
            },
        };
    }

    internal BridgeArguments Parse(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object) throw new ArgumentException("Arguments must be a JSON object.");
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in args.EnumerateObject())
        {
            var parameter = Array.Find(Parameters, p => p.Name == property.Name);
            if (parameter is null) throw new ArgumentException($"Unknown argument '{property.Name}'.");
            if (!values.TryAdd(property.Name, property.Value)) throw new ArgumentException($"Duplicate argument '{property.Name}'.");
            if (!parameter.Accepts(property.Value))
                throw new ArgumentException($"Invalid argument '{property.Name}'. {parameter.Description}");
        }
        foreach (var parameter in Parameters)
        {
            if (values.ContainsKey(parameter.Name)) continue;
            if (parameter.Default is not { } fallback) throw new ArgumentException($"Missing argument '{parameter.Name}'.");
            values.Add(parameter.Name, fallback);
        }
        return new(values);
    }
}

internal sealed class BridgeArguments(Dictionary<string, JsonElement> values)
{
    internal JsonElement Value(string name) => values[name];
    internal float Number(string name) => values[name].GetSingle();
    internal int Int(string name) => (int)values[name].GetDecimal();
    internal uint UInt(string name) => (uint)values[name].GetDecimal();
    internal string Text(string name) => values[name].GetString()!;
    internal bool Flag(string name) => values[name].GetBoolean();
}
