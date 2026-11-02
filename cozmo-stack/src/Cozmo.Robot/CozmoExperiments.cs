using System.Text;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
namespace Cozmo.Robot;

public sealed partial class CozmoEngine
{
    // fidelity: M15-049, M15-053 — context+38, Lab+8. Consumers remain M15-052.
    internal CozmoExperiments Experiments { get; private set; } = null!;
}

// Nullable fields mean constructor defaults not established by the checked setter rows.
// No consumer receives an invented zero/empty value for a member that has never been written.
internal sealed record LabVariation(byte[]? Key=null,byte? Population=null);
internal sealed record LabExperiment(uint? Version=null,byte[]? Key=null,byte? ActivationMode=null,
    byte[]? Start=null,byte[]? Stop=null,byte[]? Pause=null,byte[]? Resume=null,byte? Population=null,
    IReadOnlyList<byte[]>? AudienceTags=null,IReadOnlyList<LabVariation>? Variations=null);
internal sealed record LabDefinition(byte[]? ProjectId=null,uint? Version=null,uint? Revision=null,IReadOnlyList<LabExperiment>? Experiments=null);

// fidelity: M15-049 — checked definition loader/setters, no assignment/bucketing implementation.
internal sealed partial class CozmoExperiments
{
    private readonly CozmoEngine _engine;
    private readonly object _gate=new();
    private readonly HashSet<string> _missing=new();
    private void Missing(string text)=>SteppedBehavior.ReportMissing(text,_missing);
    private LabDefinition _definition=new(Experiments:Array.Empty<LabExperiment>());
    internal bool Enabled => Lab.Enabled;
    internal string? DeviceIdInput { get; private set; }
    internal Stream? CerrOverride;
    internal const string RelativePath="config/experiments.json";
    internal CozmoExperiments(CozmoEngine engine)
    {
        _engine=engine;
        Lab=new LabActivationRuntime{Definition=()=>Definition,Log=engine.EmitSourceLog,
            AssignmentGate=_gate,AssertLogFlush=engine.Das.ForceFlushNow};
        // Native constructor vectors +40/+4C/+58 and context subscriber+A0, before Init.
        Lab.Assignments40=new();_assignments4C=new();_assignments58=new();
        Lab.FindExperiment=key=>Definition.Experiments?.FirstOrDefault(e=>e.Key is not null&&e.Key.AsSpan().SequenceEqual(key));
        Lab.FindVariation=(experiment,key)=>experiment.Variations?.FirstOrDefault(v=>v.Key is not null&&v.Key.AsSpan().SequenceEqual(key));
        Lab.FindAssignment=(offset,key,user)=>(offset switch{0x40=>Lab.Assignments40,0x4C=>_assignments4C,0x58=>_assignments58,_=>null})?.FirstOrDefault(a=>a.Key.AsSpan().SequenceEqual(key)&&a.User.AsSpan().SequenceEqual(user));
        string[] statuses={"Invalid","ExperimentNotFound","VariantNotFound","ExperimentNotRunning","AudienceMismatch","Unassigned","Assigned","OverrideAssigned","ForceAssigned"};
        Lab.StatusName=status=>status<statuses.Length?Encoding.ASCII.GetBytes(statuses[status]):throw new LabMissingSource();
        Lab.ParseSystemDate=ParseNativeDate;
        var signal=new LabAssignmentSignal{Release4EB0CA=node=>node.RefCount20=unchecked(node.RefCount20-1)};
        _engineAssignmentNode=signal.Connect(values=>{lock(_gate)if(_startupHistoryKnown)_activeAssignments=values.Select(CopyAssignment).ToArray();});
        Lab.AssignmentSignal64=signal;_activeAssignments=Array.Empty<LabAssignmentDef>();_cachedAssignments=Array.Empty<LabAssignmentPair>();
        Audience=new LabAudienceTags{Log=engine.EmitSourceLog};
        Audience.RegisterEngineTags(()=>NeedsState8Input is {} input ? input() : _preInitNeeds.ReadTimeCreated(),()=>DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ()=>engine.Options.LabLocaleLanguage,()=>engine.Options.LabLocaleCountry);
    }
    private readonly List<LabAssignmentDef> _assignments4C;
    private readonly List<LabAssignmentDef> _assignments58;
    private readonly LabAssignmentSignalNode _engineAssignmentNode;
    private bool _startupHistoryKnown=true;
    private readonly LabPreInitNeedsRecipient _preInitNeeds=new();
    private static LabAssignmentDef CopyAssignment(LabAssignmentDef a)=>new(a.Key.ToArray(),a.User.ToArray(),a.Variation.ToArray());
    private static uint ParseNativeDate(byte[] input)
    {
        // Shipped canonical date prefix; libc strptime's broader accepted grammar is not inferred.
        if(input.Length<19||input.Take(19).Any(b=>b>0x7f))LabActivationRuntime.Missing("M15-052 noncanonical strptime/timegm input at 0x00831C68");
        string text=Encoding.ASCII.GetString(input,0,19);
        if(!DateTimeOffset.TryParseExact(text,"yyyy-MM-dd'T'HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal|System.Globalization.DateTimeStyles.AdjustToUniversal,out var date))
            LabActivationRuntime.Missing("M15-052 noncanonical strptime/timegm input at 0x00831C68");
        return unchecked((uint)date.ToUnixTimeSeconds());
    }
    internal void AttachInitializedNeeds(NeedsManager owner)=>_preInitNeeds.Attach(owner);
    internal void RetireLab(){Lab.AssignmentSignal64?.Disconnect(_engineAssignmentNode);}    // fidelity: M15-052 — checked units live on the actual CozmoExperiments context owner.
    internal LabActivationRuntime Lab {get;}
    internal LabAudienceTags Audience {get;}
    internal LabAbInterface Ab => LabAbInterface.ProcessGlobals;
    internal Func<long?>? NeedsState8Input;
    internal Func<IReadOnlyList<byte[]>>? KnownAudienceTagsInput;
    internal Func<(Delegate First,Delegate Second)>? AbCallbacksInput;
    internal Action? InitBeforeNeedsObserved;
    internal void AutoActivateForBodyId(uint bodyId)
    {
        // 537064 to_string<unsigned> -> 6A5744. Always on, including supported non-SDK paths.
        try
        {
            if(Lab.AutoActivate(Encoding.ASCII.GetBytes(bodyId.ToString(System.Globalization.CultureInfo.InvariantCulture)),Audience.GetQualified()) is null)
                lock(_gate){_startupHistoryKnown=false;_activeAssignments=null;}
        }
        catch(LabMissingSource){lock(_gate){_startupHistoryKnown=false;_activeAssignments=null;}}
    }
    // Immutable entry records and fresh byte arrays are retained internally, not exposed to production writers.
    internal LabDefinition Definition { get { lock(_gate)return _definition; } }
    internal void LoadRobotConfig()
    {
        if(_engine.Options.ResourcesPath is not {} root)
        {Missing("M15-049 resource scope1 config/experiments.json at0x00522F44 has no supplied resource path; no invented file input");return;}
        byte[] input;
        try{input=File.ReadAllBytes(root+"/"+RelativePath);}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){input=Array.Empty<byte>();}
        _=Load(input); // 0x00522F44 ignores Load's bool.
    }
    internal bool Load(byte[] input)
    {
        lock(_gate)
        {
            try
            {
                if(!Json.TryParseFirst(input,out var doc))
                {
                    _engine.EmitSourceLog(EngineLogLevel.Error,null,"AnkiLab.Load","Invalid json format in AnkiLab file");
                    EngineErrorState.StoreAndMaybeBreak();return false;
                }
                // M1-034 stages only to preserve the previous whole file on terminating malformed input.
                // On the defined success path, fields/entries are applied in the native order without reset.
                var candidate=Apply(_definition,doc.RootElement);
                _definition=candidate;return true;
            }
            catch(Exception e)when(e is JsonLogicError or JsonRuntimeError)
            {
                StackDiagnostics.Write("M1-034/M15-049 skipping malformed experiments.json; prior definitions retained; JsonCpp isMember/asString/asUInt/index or depth throw at0x008EB06E/0x008E4A04/0x008E8A8A/0x008EA6DC/0x008E0D7A: "+e.Message);return false;
            }
            catch(JsonMissingSource e){Missing("M15-049 checked JSON dependency: "+e.Message);return false;}
            catch(NotSupportedException e){Missing("M15-049 "+e.Message);return false;}
        }
    }
    private static bool Member(FirmwareJsonValue value,string key,out FirmwareJsonValue member)
    {
        if(value.Kind is not(FirmwareJsonKind.Null or FirmwareJsonKind.Object))throw new JsonLogicError("isMember requires object/null");
        member=new();return value.Kind==FirmwareJsonKind.Object&&value.Members.TryGetValue(Convert.ToHexString(Encoding.UTF8.GetBytes(key)),out member!);
    }
    private static byte[]? Text(FirmwareJsonValue v,string key,byte[]? prior)=>Member(v,key,out var m)?Json.AsStringBytes(m):prior;
    private static uint? UInt(FirmwareJsonValue v,string key,uint? prior)=>Member(v,key,out var m)?Json.AsUInt(m):prior;
    private static byte? Byte(FirmwareJsonValue v,string key,byte? prior)=>Member(v,key,out var m)?unchecked((byte)Json.AsUInt(m)):prior;
    private LabDefinition Apply(LabDefinition old,FirmwareJsonValue v)
    {
        var d=old;
        if(Member(v,"meta",out var meta))d=d with{ProjectId=Text(meta,"project_id",d.ProjectId),Version=UInt(meta,"version",d.Version),Revision=UInt(meta,"revision",d.Revision)};
        if(Member(v,"experiments",out var entries))
        {
            var next=new List<LabExperiment>();
            for(uint i=0;i<(uint)Json.Size(entries);i++)
                next.Add(ApplyExperiment(old.Experiments is {} previous && i<previous.Count?previous[(int)i]:new(),Json.Index(entries,i)));
            d=d with{Experiments=next};
        }
        return d;
    }
    private LabExperiment ApplyExperiment(LabExperiment old,FirmwareJsonValue v)
    {
        var e=old with{Version=UInt(v,"version",old.Version),Key=Text(v,"key",old.Key)};
        if(Member(v,"activation_mode",out var mode))e=e with{ActivationMode=Mode(Json.AsStringBytes(mode))};
        e=e with{Start=Text(v,"start_time_utc_iso8601",e.Start),Stop=Text(v,"stop_time_utc_iso8601",e.Stop),Pause=Text(v,"pause_time_utc_iso8601",e.Pause),Resume=Text(v,"resume_time_utc_iso8601",e.Resume),Population=Byte(v,"pop_frac_pct",e.Population)};
        if(Member(v,"audience_tags",out var tags))
        {
            var next=new List<byte[]>();for(uint i=0;i<(uint)Json.Size(tags);i++)next.Add(Json.AsStringBytes(Json.Index(tags,i)));
            e=e with{AudienceTags=next};
        }
        if(Member(v,"variations",out var variants))
        {
            var next=new List<LabVariation>();
            for(uint i=0;i<(uint)Json.Size(variants);i++)
            {
                var prior=old.Variations is {} previous&&i<previous.Count?previous[(int)i]:new();var entry=Json.Index(variants,i);
                next.Add(prior with{Key=Text(entry,"key",prior.Key),Population=Byte(entry,"pop_frac_pct",prior.Population)});
            }
            e=e with{Variations=next};
        }
        if(Member(v,"forced_variations",out var forced)&&Json.Size(forced)!=0)
            throw new NotSupportedException("nonempty forced_variations VariantCondition::SetFromJSON descendant at0x008F98AE remains unread; definition apply stopped, not a native failure result");
        return e;
    }
    private byte Mode(byte[] text)
    {
        if(text.AsSpan().SequenceEqual("manual"u8))return 0;
        if(text.AsSpan().SequenceEqual("automatic"u8))return 1;
        var stderr=CerrOverride??Console.OpenStandardError();stderr.Write("error: string '"u8);stderr.Write(text);stderr.Write("' is not a valid ActivationMode value\n"u8);stderr.Flush();return 0;
    }
    // fidelity: M15-052 — CozmoEngine::Init4EC9D6 precedes NeedsManager::Init4ECA0A.
    internal void InitBeforeNeeds(Action? initializeNeeds)
    {
        Init();
        if(initializeNeeds is {} needs)needs();
        else Missing("M15-052/M1-054 engine Init NeedsManager recipient4ECA0A (timer/config arguments) is unbound after InitExperiments4EC9D6; no later host state is substituted");
    }
    internal void Init()
    {
        // 4EC9D6: InitExperiments precedes NeedsManager::Init. This is the existing context startup entry;
        // pre-init NeedsState+8 and native engine Init recipient ownership are not manufactured from a later host needs state.
        InitBeforeNeedsObserved?.Invoke();
        try
        {
            // M15-049: force-disable starts zero and its only writer chain ends at the
            // unreachable HandleABTestingForceURL7EEB94. No app/host disable input exists.
            Lab.Enabled=true;
            _engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","AnkiLab.Enable","Enabling the lab");
            // fidelity: M15-053 — COMPATIBILITY_POLICY: explicit stable Java DAS.getDeviceID input.
            DeviceIdInput=_engine.Options.DasDeviceId;
            if(DeviceIdInput is null)LabActivationRuntime.Missing("M15-053 DAS platform user-id input0x006A5626: no explicit stable DasDeviceId setting; no generated UUID");
            var bytes=Encoding.UTF8.GetBytes(DeviceIdInput!);int nul=Array.IndexOf(bytes,(byte)0);if(nul>=0)bytes=bytes[..nul];
            if(Lab.AutoActivate(bytes,Audience.GetQualified()) is null){lock(_gate){_startupHistoryKnown=false;_activeAssignments=null;}return;}
            IReadOnlyList<byte[]> known;
            if(KnownAudienceTagsInput is {} supplied)known=supplied();
            else
            {
                var definitions=Definition.Experiments;if(definitions is null||definitions.Any(e=>e.AudienceTags is null))LabActivationRuntime.Missing("GetKnownAudienceTags definition/tag vector unavailable at 0x007EE658");
                known=definitions!.SelectMany(e=>e.AudienceTags!).ToArray();
            }
            _=Audience.Verify(known); // source concatenates all definition tags and ignores result
            if(AbCallbacksInput is {} testCallbacks){var callbacks=testCallbacks();Ab.Initialize(callbacks.First,callbacks.Second);}
            else
            {
                Action<Action<LabActivationRuntime>?> register=callback=>
                {
                    if(!_engine.NeedsContextAlive){StackDiagnostics.Write("M1-034/M15-052 skipped retired captured AB Lab callback 0x006A6112");return;}
                    if(callback is null){StackDiagnostics.Write("M1-034/M15-052 skipped empty AB callable 0x006A615A");return;}
                    callback(Lab);
                };
                Func<byte[]?> user=()=>
                {
                    if(!_engine.NeedsContextAlive){StackDiagnostics.Write("M1-034/M15-052 skipped retired captured AB ID callback 0x006A61CA");return null;}
                    var first=_engine.Robots.GetFirstRobot();
                    if(first is not null)return Encoding.ASCII.GetBytes(first.NativeBodyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if(DeviceIdInput is null){SteppedBehavior.ReportMissing("M15-053 captured DAS device ID unavailable for AB callback");return null;}
                    var data=Encoding.UTF8.GetBytes(DeviceIdInput);int end=Array.IndexOf(data,(byte)0);return end<0?data:data[..end];
                };
                Ab.Initialize(register,user);
            }
        }
        catch(LabMissingSource){lock(_gate){_startupHistoryKnown=false;_activeAssignments=null;}}
        Missing("M15-050/M15-016 SetGameBeingPaused(false) resume writeback at0x006990DA: host pause/resume delivery and Needs resume caller are unbuilt; no synthetic resume or periodic write substituted");
    }
}
