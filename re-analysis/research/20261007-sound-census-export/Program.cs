// Navigation/metadata export only. Not an independent oracle or fidelity verifier.
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cozmo.Robot.Animation.Wwise;

var options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = false };
options.Converters.Add(new FloatBits());
options.Converters.Add(new DoubleBits());
using var archive = ZipFile.OpenRead(args[0]);
using var output = new StreamWriter(args[1], false, new System.Text.UTF8Encoding(false));
foreach (var entry in archive.Entries.Where(e => e.FullName.EndsWith(".bnk", StringComparison.Ordinal)).OrderBy(e => e.FullName))
{
    using var stream = entry.Open(); using var buffer = new MemoryStream(); stream.CopyTo(buffer);
    var bank = WwiseBank.Parse(buffer.ToArray(), entry.FullName);
    output.WriteLine(JsonSerializer.Serialize(new { kind="bank", bank=entry.FullName, bank.BankId, bank.Version, bank.Stmg }, options));
    foreach (var obj in bank.Objects.Values.OrderBy(o => o.Id))
    {
        var node = WwiseHierarchy.TryRead(obj, out var problem);
        object? data = node is null ? null : JsonSerializer.SerializeToElement(node, node.GetType(), options);
        output.WriteLine(JsonSerializer.Serialize(new { kind="object", bank=entry.FullName, obj.Id, type=(int)obj.Type,
            node=data, problem, actions=obj.Type==WwiseObjectType.Event ? WwiseBank.EventActions(obj) : null,
            actionType=obj.Type==WwiseObjectType.EventAction ? BitConverter.ToUInt16(obj.Payload.Span[4..6]) : (ushort?)null,
            actionTarget=obj.Type==WwiseObjectType.EventAction ? BitConverter.ToUInt32(obj.Payload.Span[6..10]) : (uint?)null }, options));
    }
}

sealed class FloatBits : JsonConverter<float>
{
    public override float Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotSupportedException();
    public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options) => writer.WriteStringValue(BitConverter.SingleToUInt32Bits(value).ToString("X8"));
}
sealed class DoubleBits : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotSupportedException();
    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) => writer.WriteStringValue(BitConverter.DoubleToUInt64Bits(value).ToString("X16"));
}
