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

internal static class NotificationDeterminantCodec
{
    internal static byte[] Pack(NotificationDeterminants input)
    {
        if(input.UnlockTimestamps.Count!=85||input.HasPlayed.Count!=85)throw new ArgumentException("Notification history requires exactly 85 values in each vector.",nameof(input));
        byte[] data=new byte[433];BinaryPrimitives.WriteInt32LittleEndian(data,input.InstallSeconds);BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4),85);
        for(int i=0;i<85;i++)BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(6+i*4),input.UnlockTimestamps[i]);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(346),85);for(int i=0;i<85;i++)data[348+i]=input.HasPlayed[i]?(byte)1:(byte)0;
        return data;
    }
}

// fidelity: M15-093 — bounded shipped LocalNotifications path; nonshipped branches remain explicit gaps.
internal sealed class LocalNotifications : IDisposable
{
    private sealed class Item
    {
        internal int? Kind,Connection,When,Days,Unlock,Comparison;
        internal string Title="",Parameter="",Deeplink="",Url="";
        internal List<string> Texts=new();
        internal float? Early,Late,Minimum,NoEarlier,NoLater;
        // The reused union storage can alias prior variants; missing union scalars
        // remain unavailable until that changed-config case is source-bounded.
        internal Item Next()=>new(){Kind=255,Connection=Connection,When=When,Early=Early,Late=Late,Minimum=Minimum,NoEarlier=NoEarlier,NoLater=NoLater};
    }
    private readonly CozmoEngine _engine;
    private readonly NeedsManager _needs;
    private readonly EngineRandom _rng;
    private readonly List<IDisposable> _subscriptions=new();
    private readonly List<Item> _items=new();
    private readonly HashSet<string> _missingScope=new();
    private NotificationDeterminants? _history;
    private int _latestId,_latestTime;
    private float _deadline;
    private bool _retired,_rngHistoryKnown=true;
    internal bool RandomHistoryKnown=>_rngHistoryKnown;
    internal void MarkHistoryUnavailable()=>_rngHistoryKnown=false;
    internal float Deadline=>_deadline;
    internal int LatestId=>_latestId;
    internal int LatestTime=>_latestTime;
    internal bool AnyMinigamePlayed=>_history is {} h&&(h.HasPlayed[15]||h.HasPlayed[17]||h.HasPlayed[18]);
    internal int ItemCount=>_items.Count;
    internal bool TryEvaluateForTest(int index,out bool eligible)=>Eligible(_items[index],out eligible);
    private bool Current=>!_retired&&_engine.NeedsContextAlive&&ReferenceEquals(_engine.Needs,_needs);
    internal LocalNotifications(CozmoEngine engine,NeedsManager needs)
    {_engine=engine;_needs=needs;_rng=engine.ContextRandom;}

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
        catch(JsonMissingSource e){Missing("notification config conversion unavailable: "+e.Message);return false;}
        catch(Exception e) when(e is JsonLogicError or JsonRuntimeError)
        {Missing("M1-034 notification config item skipped at 0x00789E30: "+e.Message);return false;}
        if(!_engine.NeedsContextExternalInterfaceAvailable){Missing("M1-034 null notification subscription interface at 0x0068C86E");return false;}
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
    private void Missing(string text)=>SteppedBehavior.ReportMissing("M15-093 LocalNotifications: "+text,_missingScope);
    private bool Feature(out bool enabled)
    {if(_engine.Features is null){enabled=false;Missing("feature 11 input unavailable");return false;}enabled=_engine.Features.IsEnabled("LocalNotifications");return true;}
    private bool TryNowSeconds(out int seconds)
    {
        long value=_engine.NeedsWallClockMicros()/1_000_000L;
        if(!Current){seconds=0;return false;}
        if(value<int.MinValue||value>int.MaxValue){seconds=0;Missing("clock outside checked signed32 notification domain");return false;}
        seconds=(int)value;return true;
    }
    private static byte[] Text(string text)=>Encoding.UTF8.GetBytes(text);
    private static void PutString(Stream stream,string text){byte[] bytes=Text(text);int count=unchecked((byte)bytes.Length);stream.WriteByte((byte)count);stream.Write(bytes,0,count);}
    private void SendTextKeys()
    {
        if(!Current)return;
        if(!_engine.NeedsContextExternalInterfaceAvailable){Missing("M1-034 null text-key report interface at 0x0068CE52");return;}
        string[] texts=_items.SelectMany(i=>i.Texts).ToArray(),titles=_items.Select(i=>i.Title).Where(s=>s.Length!=0).ToArray();
        using var stream=new MemoryStream();void PutVector(string[] a){Span<byte> count=stackalloc byte[2];BinaryPrimitives.WriteUInt16LittleEndian(count,unchecked((ushort)a.Length));stream.Write(count);foreach(string s in a.Take(unchecked((ushort)a.Length)))PutString(stream,s);}
        PutVector(texts);PutVector(titles);_engine.DeliverNurture(0x99,stream.ToArray(),new NotificationTextKeysReport(texts,titles));
    }
    private bool TryLocalDate(string text,out int seconds)
    {
        seconds=0;
        if(!DateTime.TryParseExact(text,"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var value))
        {Missing("noncanonical local-date/mktime input at 0x0068D628");return false;}
        if(!NativeNotificationCalendar.TryMktime(value,out seconds)){Missing("host C mktime recipient unavailable for notification date");return false;}
        return true;
    }
    private bool TryMinutes(string value,out float minutes)
    {
        if(value.Length>0&&value.All(c=>c>='0'&&c<='9')&&float.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out minutes)&&float.IsFinite(minutes)&&minutes<=259200f)return true;
        minutes=0;Missing("notification stof input outside checked shipped decimal domain");return false;
    }
    private bool TargetFuture(string parameter,int origin,out bool result)
    {
        result=false;if(!TryNowSeconds(out int now)||!TryMinutes(parameter,out float minutes))return false;
        int delta=(int)(minutes*60f);result=unchecked(origin+delta)>now;return true;
    }
    private bool Eligible(Item item,out bool result)
    {
        result=false;if(item.Connection is not {} connection||item.Kind is not {} kind||item.When is not {} when){Missing("uninitialized notification predicate field");return false;}
        if((!_needs.NativeNotificationDidConnect&&connection==1)||(_needs.NativeNotificationDidConnect&&connection==2))return true;
        if(kind==0)
        {
            result=true;
            if(when==4){if(!TryNowSeconds(out int now)||!TryLocalDate(item.Parameter,out int target))return false;result=target>now;}
            if(!Feature(out bool enabled))return false;
            if(enabled&&when==5){if(_history is null){Missing("persisted notification determinant unavailable");return false;}return TargetFuture(item.Parameter,_history.InstallSeconds,out result);}
            return true;
        }
        if(kind==4)
        {
            if(!TryNowSeconds(out int now))return false;if(_history is null||item.Days is not {} days){Missing("post-onboarding determinant unavailable");return false;}
            result=now>unchecked(_history.InstallSeconds+days*86400);return true;
        }
        if(kind==5)
        {
            if(_history is null||item.Unlock is not {} id||item.Comparison is not {} comparison){Missing("trick notification determinant unavailable");return false;}
            if((uint)id>=85){Missing("M1-034 notification unlock index outside native fixed history at 0x0068D1DC");return false;}
            if(comparison==1){if(_latestId==0||_latestId!=id||_latestTime==0)return true;}
            else if(comparison is 2 or 3)
            {if(_history.UnlockTimestamps[id]<1||_history.HasPlayed[id]!=(comparison==2))return true;}
            else return true;
            return TargetFuture(item.Parameter,_latestTime,out result);
        }
        if(kind>6){result=true;return true;}
        Missing("notification predicate type outside checked shipped domain");return false;
    }
    private bool TrySchedule(Item item,long captured,out int seconds)
    {
        seconds=0;if(item.When is not {} when){Missing("notification timing type unavailable");return false;}
        float minutes;
        if(when==2){if(!TryMinutes(item.Parameter,out minutes))return false;}
        else if(when==4)
        {
            if(!TryLocalDate(item.Parameter,out int target))return false;long capturedSeconds=captured/1_000_000L;
            if(capturedSeconds<int.MinValue||capturedSeconds>int.MaxValue){Missing("captured clock outside checked signed32 notification domain");return false;}
            minutes=(float)unchecked(target-(int)capturedSeconds)/60f;if(item.Minimum is not {} min){Missing("notification minimum unavailable");return false;}minutes=MathF.Max(minutes,(float)(int)min);
        }
        else if(when is 5 or 6)
        {
            if(_history is null){Missing("notification scheduling determinant unavailable");return false;}
            if(!TryMinutes(item.Parameter,out float value))return false;int origin=when==5?_history.InstallSeconds:_latestTime;if(!TryNowSeconds(out int now))return false;
            int target=unchecked(origin+(int)(value*60f));minutes=(float)unchecked(target-now)/60f;if(item.Minimum is not {} min){Missing("notification minimum unavailable");return false;}minutes=MathF.Max(minutes,(float)(int)min);
        }
        else {Missing("notification scheduling type outside checked shipped domain");return false;}
        if(item.Early is not {} early||item.Late is not {} late){Missing("notification timing range unavailable");return false;}
        if(!_rngHistoryKnown){Missing("shared RNG history unavailable after an earlier incomplete generation");return false;}
        float draw=(float)_rng.RandDblInRange(-(double)early,(double)late);minutes=minutes+draw;
        if(item.Minimum is not {} minimum){Missing("notification minimum unavailable");return false;}
        if(!float.IsFinite(minutes)||!float.IsFinite(minimum)){Missing("notification arithmetic outside checked finite timing domain");return false;}
        if(minutes<minimum)minutes=minimum;
        if(_needs.NativeNotificationMaxFuture is not {} maximum){Missing("live NeedsConfig notification maximum unavailable");return false;}
        if(!float.IsFinite(maximum)){Missing("notification maximum outside checked finite timing domain");return false;}
        if(minutes>maximum){_engine.EmitSourceLog(EngineLogLevel.Info,null,"LocalNotifications.DetermineTimeToNotify","Notification exceeds maximum future time");if(!Current)return false;minutes=maximum;}
        float secondsFloat=minutes*60f;
        if(!float.IsFinite(secondsFloat)||secondsFloat>=2147483648f||secondsFloat< -2147483648f){Missing("notification seconds outside checked signed32 timing domain");return false;}
        seconds=(int)secondsFloat;
        long targetSeconds=unchecked((captured+(long)seconds*1_000_000L)/1_000_000L);
        if(!NativeNotificationCalendar.TryLocalMinute(targetSeconds,out float localMinutes)){Missing("host C localtime recipient unavailable for notification date");return false;}
        if(item.NoEarlier is not {} lower){Missing("notification lower time window unavailable");return false;}
        if(!float.IsFinite(lower)){Missing("notification lower window outside checked finite domain");return false;}
        if(localMinutes<lower)seconds=(int)(((lower-localMinutes)*60f)+(float)seconds);
        else
        {
            if(item.NoLater is not {} upper){Missing("notification upper time window unavailable");return false;}
            if(!float.IsFinite(upper)){Missing("notification upper window outside checked finite domain");return false;}
            if(localMinutes>upper){int intermediate=(int)(((1440f-localMinutes)*60f)+(float)seconds);seconds=(int)((float)intermediate+lower*60f);}
        }
        return true;
    }
    internal bool TryGenerate()
    {
        if(!Current)return false;if(!Feature(out bool enabled)){_rngHistoryKnown=false;return false;}if(!enabled)return true;
        if(!_engine.NeedsContextExternalInterfaceAvailable)Missing("M1-034 null clear-cache interface at 0x0068CAD0; report skipped");
        else _engine.DeliverNurture(0x97,Array.Empty<byte>(),null);
        if(!Current)return false;
        if(!_needs.NativeNotificationNeedsConfigAvailable){Missing("actual Needs config unavailable after Clear at 0x0068CAEA");_rngHistoryKnown=false;return false;}
        _needs.State.UpdateCurNeedsBrackets();if(!Current)return false;
        if(!_needs.NativeNotificationDecayAvailable){Missing("actual decay input unavailable after bracket refresh at 0x0068CAFC");_rngHistoryKnown=false;return false;}
        _= _needs.Decay.DecayMultipliers(n=>_needs.State.GetNeedLevel(n));if(!Current)return false;
        long captured=_engine.NeedsWallClockMicros();if(!Current)return false;_= (float)((captured-_needs.State.NativeAppOpenMicros)/1_000_000L)/60f;
        foreach(var item in _items)
        {
            if(!Eligible(item,out bool eligible)){_rngHistoryKnown=false;return false;}if(!Current)return false;if(!eligible)continue;
            if(!TrySchedule(item,captured,out int seconds)){_rngHistoryKnown=false;return false;}if(!Current)return false;
            int choice=_rng.RandInt(item.Texts.Count);
            if((uint)choice>=(uint)item.Texts.Count){Missing("M1-034 notification text read outside vector at 0x0068CB98; item skipped");if(!Current)return false;continue;}
            string text=item.Texts[choice];using var stream=new MemoryStream();Span<byte> word=stackalloc byte[4];BinaryPrimitives.WriteInt32LittleEndian(word,seconds);stream.Write(word);
            PutString(stream,item.Title);PutString(stream,text);stream.WriteByte(0);PutString(stream,item.Deeplink);PutString(stream,item.Url);
            if(!_engine.NeedsContextExternalInterfaceAvailable)Missing("M1-034 null cache report interface at 0x0068CC28; report skipped");
            else _engine.DeliverNurture(0x98,stream.ToArray(),new NotificationScheduleReport(seconds,item.Title,text,false,item.Deeplink,item.Url));
            if(!Current)return false;
        }
        _deadline=_needs.NowSec+60f;return true;
    }
    internal void Update(float now){if(_deadline<now)TryGenerate();}
    internal void SetPaused(bool paused)=>TryGenerate();
    public void Dispose(){_retired=true;for(int i=_subscriptions.Count-1;i>=0;i--)_subscriptions[i].Dispose();_subscriptions.Clear();}
}
