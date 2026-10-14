using System.Globalization;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
namespace Cozmo.Robot.Vision;

// fidelity: M11-065, M11-066, M11-067, M11-068
internal static class PoseDiagnostics
{
    private sealed record Context(SourceLogSink Sink,Action? Flush);
    private static readonly AsyncLocal<Context?> Current=new();
    private sealed class Scope(Context? prior):IDisposable {public void Dispose()=>Current.Value=prior;}
    internal static IDisposable Use(SourceLogSink sink,Action? flush=null)
    {var old=Current.Value;Current.Value=new(sink,flush);return new Scope(old);}
    internal static void Missing(string text)
    {if(Current.Value is {} context)context.Sink(EngineLogLevel.Warning,null,"MISSING",text);else SteppedBehavior.ReportMissing(text);}
    internal static void Warning(string key,string text)=>Log(EngineLogLevel.Warning,key,text);
    internal static void Error(string key,string text){Log(EngineLogLevel.Error,key,text);EngineErrorState.StoreAndMaybeBreak();}
    internal static void Verify(string key,string text)
    {
        Log(EngineLogLevel.Error,key,text);EngineErrorState.ErrorFlagSet=true;
        if(Current.Value?.Flush is {} flush)flush();
        else Missing("M11-066/M11-067 sVerifyFailedReturnFalse DASForceFlushNow has no supplied source sink/flush");
    }
    private static void Log(EngineLogLevel level,string key,string text)
    {
        if(Current.Value is {} context)context.Sink(level,null,key,text);
        else Missing("M11-065/M11-066/M11-067/M11-068 unbound geometry source log: "+key+": "+text);
    }
    // System DAS %f norm/axis diagnostic only (unshipped formatter). Row-element to_string is M11-083, never formatted here.
    internal static string Fixed(float value)=>float.IsNaN(value)?"nan":float.IsPositiveInfinity(value)?"inf":float.IsNegativeInfinity(value)?"-inf":value.ToString("F6",CultureInfo.InvariantCulture);
    internal static string Integer(uint value)=>unchecked((int)value).ToString(CultureInfo.InvariantCulture);
    internal static string CString(string value){int end=value.IndexOf('\0');return end<0?value:value[..end];}
    internal static void Loop(string file,int line)=>Error("LoopBoundOverflow","../../../../coretech/common/include/anki/common/basestation/math/"+file+":"+line.ToString(CultureInfo.InvariantCulture));
}
