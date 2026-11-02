using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot;

/// <summary>The persisted app-owned notification history, supplied before the first host-triggered generation.</summary>
public sealed record NotificationDeterminants(int InstallSeconds,IReadOnlyList<int> UnlockTimestamps,IReadOnlyList<bool> HasPlayed);
public sealed record NotificationTextKeysReport(IReadOnlyList<string> TextKeys,IReadOnlyList<string> TitleKeys);
public sealed record NotificationScheduleReport(int Seconds,string Title,string Text,bool Persist,string Deeplink,string Url);

// fidelity: M15-093 — bounded shipped LocalNotifications path; nonshipped branches remain explicit gaps.
internal sealed class LocalNotifications : IDisposable
{
    private sealed class Item
    {
        internal int? Kind,Connection,When,Days,Unlock,Comparison;
        internal string Title="",Parameter="",Deeplink="",Url="";
        internal List<string> Texts=new();
        internal float? Early,Late,Minimum,NoEarlier,NoLater;
        internal Item Next()=>new(){Kind=255,Connection=Connection,When=When,Days=Days,Unlock=Unlock,Comparison=Comparison,Early=Early,Late=Late,Minimum=Minimum,NoEarlier=NoEarlier,NoLater=NoLater};
    }
    private readonly CozmoEngine _engine;
    private readonly NeedsManager _needs;
    private readonly EngineRandom _rng;
    private readonly List<IDisposable> _subscriptions=new();
    private readonly List<Item> _items=new();
    private readonly float? _maximum;
    private NotificationDeterminants? _history;
    private int _latestId,_latestTime;
    private float _deadline;
    private bool _retired,_rngHistoryKnown=true;
    internal bool RandomHistoryKnown=>_rngHistoryKnown;
    internal float Deadline=>_deadline;
    private bool Current=>!_retired&&_engine.NeedsContextAlive&&ReferenceEquals(_engine.Needs,_needs);
    internal LocalNotifications(CozmoEngine engine,NeedsManager needs,float? maximum)
    {_engine=engine;_needs=needs;_rng=engine.ContextRandom;_maximum=maximum;}

    private static bool Has(FirmwareJsonValue value,string key)=>value.Kind==FirmwareJsonKind.Object&&value.Members.ContainsKey(Convert.ToHexString(Encoding.UTF8.GetBytes(key)));
    private static string Str(FirmwareJsonValue value)=>Encoding.UTF8.GetString(Json.AsStringBytes(value));
    private static int EnumName(string name,params string[] names){int i=Array.IndexOf(names,name);return i<0?0:i;}
    internal bool Initialize(string? config,float engineTime)
    {
        _deadline=engineTime+60f;_items.Clear();
        if(config is null){Missing("local notification config input unavailable");return false;}
        try
        {
            if(!Json.TryParseFirst(Encoding.UTF8.GetBytes(config),out var doc)){Missing("local notification config parse unavailable");return false;}
            using(doc)
            {
                var list=Json.Member(doc.RootElement,"localNotificationConfigs");var previous=new Item();
                if(list.Kind==FirmwareJsonKind.Array)foreach(var value in list.Elements)
                {
                    var item=previous.Next();
                    if(Has(value,"notificationMainUnion"))
                    {
                        var u=Json.Member(value,"notificationMainUnion");item.Kind=255;
                        if(Has(u,"type"))item.Kind=Str(Json.Member(u,"type")) switch{"notificationGeneral"=>0,"notificationPostOnboarding"=>4,"notificationTrickBased"=>5,_=>255};
                        if(item.Kind==4&&Has(u,"numDaysStartAfter"))item.Days=Json.AsInt(Json.Member(u,"numDaysStartAfter"));
                        if(item.Kind==5)
                        {
                            if(Has(u,"trickUnlockId"))item.Unlock=unchecked((int)NativeUnlockNames.FromBytes(Json.AsStringBytes(Json.Member(u,"trickUnlockId"))));
                            if(Has(u,"comparisonType"))item.Comparison=Str(Json.Member(u,"comparisonType")) switch{"LastUnlock"=>1,"Played"=>2,"NotPlayed"=>3,_=>1};
                        }
                    }
                    if(Has(value,"connection"))item.Connection=EnumName(Str(Json.Member(value,"connection")),"Either","DidConnect","DidNotConnect");
                    if(Has(value,"titleKey"))item.Title=Str(Json.Member(value,"titleKey"));
                    if(Has(value,"textKeys")){var texts=Json.Member(value,"textKeys");for(uint i=0;i<Json.Size(texts);i++)item.Texts.Add(Str(Json.Index(texts,i)));}
                    if(Has(value,"whenType"))item.When=EnumName(Str(Json.Member(value,"whenType")),"NotApplicable","AfterAppOpen","AfterAppClose","ClockTime","LocalDateTime","InstallDate","AfterUnlock");
                    if(Has(value,"whenParam"))item.Parameter=Str(Json.Member(value,"whenParam"));
                    if(Has(value,"rangeEarly"))item.Early=Json.AsFloat(Json.Member(value,"rangeEarly"));
                    if(Has(value,"rangeLate"))item.Late=Json.AsFloat(Json.Member(value,"rangeLate"));
                    if(Has(value,"minimumDuration"))item.Minimum=Json.AsFloat(Json.Member(value,"minimumDuration"));
                    if(Has(value,"noEarlierThan"))item.NoEarlier=Json.AsFloat(Json.Member(value,"noEarlierThan"));
                    if(Has(value,"noLaterThan"))item.NoLater=Json.AsFloat(Json.Member(value,"noLaterThan"));
                    if(Has(value,"deeplinkCodeLab"))item.Deeplink=Str(Json.Member(value,"deeplinkCodeLab"));
                    if(Has(value,"webpageURL"))item.Url=Str(Json.Member(value,"webpageURL"));
                    _items.Add(item);previous=item;
                }
            }
        }
        catch(Exception e) when(e is JsonLogicError or JsonRuntimeError or JsonMissingSource)
        {Missing("M1-034 notification config item skipped at 0x00789E30: "+e.Message);return false;}
        if(!_engine.NeedsContextExternalInterfaceAvailable){Missing("M1-034 null notification subscription interface at 0x0068C784");return false;}
        _subscriptions.Add(_engine.AnimationMessages.Subscribe(0xD4,false,()=>TryGenerate()));
        _subscriptions.Add(_engine.AnimationMessages.Subscribe(0xD5,false,SendTextKeys));
        _subscriptions.Add(_engine.AnimationMessages.SubscribeMessage(0xD6,false,p=>{if(p is NotificationDeterminants value)SetDeterminants(value);}));
        return true;
    }
    internal bool SetDeterminants(NotificationDeterminants value)
    {
        if(!Current)return false;
        if(value.UnlockTimestamps.Count!=85||value.HasPlayed.Count!=85){Missing("notification determinant requires 85 timestamps and 85 played values");return false;}
        int[] stamps=value.UnlockTimestamps.ToArray();bool[] played=value.HasPlayed.ToArray();_latestId=0;_latestTime=0;
        for(int i=0;i<stamps.Length;i++)if(stamps[i]>_latestTime){_latestTime=stamps[i];_latestId=i;}
        _history=new(value.InstallSeconds,stamps,played);return true;
    }
    private void Missing(string text)=>SteppedBehavior.ReportMissing("M15-093 LocalNotifications: "+text);
    private bool Feature(out bool enabled)
    {if(_engine.Features is null){enabled=false;Missing("feature 11 input unavailable");return false;}enabled=_engine.Features.IsEnabled("LocalNotifications");return true;}
    private int NowSeconds()=>unchecked((int)(_engine.NeedsWallClockMicros()/1_000_000L));
    private static byte[] Text(string text)=>Encoding.UTF8.GetBytes(text);
    private static void PutString(Stream stream,string text){byte[] bytes=Text(text);int count=unchecked((byte)bytes.Length);stream.WriteByte((byte)count);stream.Write(bytes,0,count);}
    private void SendTextKeys()
    {
        if(!Current)return;
        if(!_engine.NeedsContextExternalInterfaceAvailable){Missing("M1-034 null text-key report interface at 0x0068CE64");return;}
        string[] texts=_items.SelectMany(i=>i.Texts).ToArray(),titles=_items.Select(i=>i.Title).Where(s=>s.Length!=0).ToArray();
        using var stream=new MemoryStream();void PutVector(string[] a){Span<byte> count=stackalloc byte[2];BinaryPrimitives.WriteUInt16LittleEndian(count,unchecked((ushort)a.Length));stream.Write(count);foreach(string s in a.Take(unchecked((ushort)a.Length)))PutString(stream,s);}
        PutVector(texts);PutVector(titles);_engine.DeliverNurture(0x99,stream.ToArray(),new NotificationTextKeysReport(texts,titles));
    }
    internal void Update(float now){if(_deadline<now)TryGenerate();}
    internal void SetPaused(bool paused)=>TryGenerate();
    public void Dispose(){_retired=true;for(int i=_subscriptions.Count-1;i>=0;i--)_subscriptions[i].Dispose();_subscriptions.Clear();}
}
