using System.Globalization;
using System.Text.Json;
using Cozmo.Protocol;
namespace Cozmo.Robot;

// fidelity: M2-020 — operator-kept diagnostics, best effort; no claim of the native TracePrinter format interpreter.
public sealed partial class EngineRobot
{
    private bool _traceTablesAttempted;
    private IReadOnlyDictionary<int,string>? _traceNames, _traceFormats;
    private void HandleTraceDiagnostic(PrintTrace message)
    {
        if (!_traceTablesAttempted)
        {
            _traceTablesAttempted=true;
            if (Engine.Options.ResourcesPath is {} root)
            {
                string path=Path.Combine(root,"config/engine/AnkiLogStringTables.json");
                try
                {
                    using var document=JsonDocument.Parse(File.ReadAllBytes(path));
                    _traceNames=ReadTable(document.RootElement,"nameTable");
                    _traceFormats=ReadTable(document.RootElement,"formatTable");
                }
                catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
                { Engine.EmitStackLog($"Robot.Trace: cannot read string tables [{path}]: {e.Message}; raw identifiers retained"); }
            }
        }
        // Existing capture decoder resolves low16; keep the complete word and level visible for diagnosis.
        Engine.EmitStackLog($"Robot.Trace level={message.Level} formatWord=0x{message.FormatId:X8} {message.Format(_traceNames,_traceFormats)}");
    }
    private static IReadOnlyDictionary<int,string> ReadTable(JsonElement root,string key)
    {
        var result=new Dictionary<int,string>();
        if(root.TryGetProperty(key,out var table)&&table.ValueKind==JsonValueKind.Object)
            foreach(var entry in table.EnumerateObject())
                if(int.TryParse(entry.Name,NumberStyles.None,CultureInfo.InvariantCulture,out int id)&&entry.Value.ValueKind==JsonValueKind.String)
                    result[id]=entry.Value.GetString()!;
        return result;
    }
    private void HandleWifiFlashDiagnostic(WiFiFlashID message)
    {
        // OV126 routine5,0x0053CD9C..0x0053CE3A: telemetry only, no robot state or output.
        Engine.EmitSourceLog(EngineLogLevel.Event,null,"hardware.wifi.flash_id",message.Field0.ToString("x",CultureInfo.InvariantCulture),
            new Dictionary<string,string>{{"$data",message.Field0.ToString(CultureInfo.InvariantCulture)}});
    }
}
