// fidelity: M6-022
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The output-device node's per-device sink (M6-022 V3/N1, D1..D4; C12 report voice-callees Q1).
///
/// The render body's pre-loop walks the global output-device list and, per node, loads the sub-object at
/// <c>[node+0x70]</c> and calls its <c>vt+0x2c</c>, then, when that is non-zero, its <c>vt+0x30</c>. C12
/// resolves those two slots on the node class vtable <c>0x103B498</c>:
/// <list type="bullet">
/// <item><b><c>vt+0x2c = 0x9E935C</c></b> returns <c>[this+0x84]</c> — the per-node "ready" byte.</item>
/// <item><b><c>vt+0x30 = 0x9E9420</c></b> returns <b>2</b> when <c>[this+0x84]==0</c>, else <c>sem_post</c>s
/// the output-device semaphore and returns <b>1</b>.</item>
/// <item><b><c>vt+0x20</c></b> (D2) yields the per-device frame count, whose sink value is
/// <b>HARDWARE_ONLY</b> (D4): without the phone's audio device the value is not derivable from the shipped
/// artifact. It is a caller input here.</item>
/// </list>
/// The <c>[node+0x70]</c> sub-object identity and its own vtable store were not located (voice-callees Q1
/// caveat); its class is UNKNOWN, so the node is a caller-supplied object rather than a modelled class.
/// </summary>
public interface IWwiseOutputDevice
{
    /// <summary>V3/N1 <c>vt+0x2c = 0x9E935C</c>: the ready byte <c>[this+0x84]</c>.</summary>
    bool IsReady { get; }

    /// <summary>
    /// V3/N1 <c>vt+0x30 = 0x9E9420</c>: 2 when not ready, else the <c>sem_post</c> and 1.
    /// </summary>
    int Kick();

    /// <summary>
    /// D2/D4 <c>vt+0x20</c>: the device's frame count. <b>HARDWARE_ONLY</b> — the OpenSL sink's frame
    /// count/pacing is phone hardware; the caller supplies it.
    /// </summary>
    int FramesAvailable { get; }

    /// <summary>
    /// G8 <c>0x9EBA54</c>: the **list node's** state byte <c>[node+0x88]</c> (verifier finding 5;
    /// <c>0x9EBB68 ldr r3,[r5,#0x88]</c>), not the device object's <c>+0x84</c>. It holds 0, 1 or 2:
    /// <c>0x9EC4C4..0x9EC4D0</c> skips nodes already in {1,2}, and the inline walk sets the others to 2.
    /// </summary>
    int AdvanceState { get; set; }
}

/// <summary>
/// The output-device module state and gates (M6-022 G1..G10, D1..D4). One struct at <c>0x108DAE8</c>:
/// gate1 <c>0x108DAF0</c> at <c>+8</c>, the device count <c>0x108DAFC</c> at <c>+0x14</c>, the device-list
/// head <c>0x108DB04</c> at <c>+0x1C</c>, gate2 <c>0x108DB08</c> at <c>+0x20</c>, the countdown
/// <c>0x108DB0C</c> at <c>+0x24</c>, the flag <c>0x108DB18</c> at <c>+0x30</c>. Gate3 <c>0x1052430</c> is
/// written alongside gate2 (G4). The runtime gate values depend on the phone's Wwise output device and are
/// <b>HARDWARE_ONLY</b>; the writers and the reader are settled and are modelled here.
/// </summary>
public sealed class WwiseOutputDeviceState
{
    /// <summary>G1 <c>+8</c>: gate1 <c>0x108DAF0</c>.</summary>
    public byte Gate1 { get; private set; }

    /// <summary>G1 <c>+0x20</c>: gate2 <c>0x108DB08</c>.</summary>
    public byte Gate2 { get; private set; }

    /// <summary>G4: gate3 <c>0x1052430</c>, written with gate2.</summary>
    public byte Gate3 { get; private set; }

    /// <summary>G1 <c>+0x24</c>: the SetOutputDevice countdown <c>0x108DB0C</c>.</summary>
    public int Countdown { get; private set; }

    /// <summary>G1 <c>+0x30</c>: the term flag <c>0x108DB18</c>.</summary>
    public byte Flag { get; private set; }

    /// <summary>G1 <c>+0x14</c>: the device count <c>0x108DAFC</c> (the list is caller-owned).</summary>
    public int DeviceCount { get; private set; }

    private readonly List<IWwiseOutputDevice> _devices = new();

    /// <summary>G9: the manager tick <c>[0x108D870]+0x4C</c>; the caller advances it.</summary>
    public long ManagerTick { get; set; }

    /// <summary>
    /// V4/G10: the bus-pass argument is <c>(gate2==0) ? 1 : gate3</c>. Both gates are dynamic (G2..G5);
    /// the code path is settled, the runtime value is HARDWARE_ONLY.
    /// </summary>
    public int BusPassArg => Gate2 == 0 ? 1 : Gate3;

    /// <summary>G6 <c>0x9EADE8</c>: gate2 = 0, countdown = 0, gate3 = 1, device list empty.</summary>
    public void Init()
    {
        Gate2 = 0;
        Countdown = 0;
        Gate3 = 1;
        _devices.Clear();
        DeviceCount = 0;
        Gate1 = 0;
        Flag = 0;
    }

    /// <summary>
    /// G7 <c>0x9EAF90</c>: empty the device list, reset the count, gate1 = 0,
    /// <c>0x108DAE8 = 1</c>, <c>0x108DB18 = 0</c>. The two device gains <c>0x108DAF4</c>/<c>0x108DAF8</c>
    /// (set to 1.0) are not modelled here; the thread post/join and sem destroy are the caller's
    /// (this stack has no audio thread; M6-017's caller seam owns it).
    /// </summary>
    public void Term()
    {
        _devices.Clear();
        DeviceCount = 0;
        Gate1 = 0;
        Flag = 0;
    }

    /// <summary>Adds a device to the list the render body walks (the list head is <c>0x108DB04</c>).</summary>
    public void AddDevice(IWwiseOutputDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _devices.Add(device);
        DeviceCount = _devices.Count;
    }

    /// <summary>G9: the list the render body and <c>0x9EBE6C</c> iterate.</summary>
    public IReadOnlyList<IWwiseOutputDevice> Devices => _devices;

    /// <summary>
    /// V3/N1 pre-loop: per device, call <c>vt+0x2c</c>; when non-zero call <c>vt+0x30</c> (voice-callees Q1).
    /// </summary>
    public void PreLoop()
    {
        foreach (var device in _devices)
            if (device.IsReady)
                device.Kick();
    }

    /// <summary>
    /// D1..D4 <c>0x9EBE6C(param)</c>: walk the devices, read each <c>vt+0x20</c> frame count, take the
    /// minimum into the return, and write gate1. The no-device branch sets gate1 = 1
    /// (<c>0x9EC1A4..0x9EC1B4</c>) but returns 0 (<c>0x9EC1BC mov r0,#0</c>). The device path zeroes
    /// <c>sl</c> (<c>0x9EC15C..0x9EC170</c>) and stores it at <c>0x9EC00C</c>, so gate1 = 0 there. The
    /// count value is <b>HARDWARE_ONLY</b> (D4).
    /// </summary>
    public int AdvanceDevices(int param)
    {
        if (_devices.Count == 0)
        {
            Gate1 = 1;                                  // 0x9EC1A4..0x9EC1B4
            return 0;                                   // 0x9EC1BC mov r0,#0
        }

        int min = int.MaxValue;
        foreach (var device in _devices)
            min = Math.Min(min, device.FramesAvailable);
        if (min == int.MaxValue) min = 0;
        Gate1 = 0;                                      // 0x9EC15C..0x9EC170 mov sl,#0; 0x9EC00C strb
        return min;
    }

    /// <summary>
    /// G8 <c>0x9EBA54(param_1)</c>: per-frame device advance. For a list node whose <c>[node+0x88]</c> state
    /// is non-zero, a PBI/GetJSON object is created and the byte is reset (caller seam; the object identity
    /// is not in the rows here). Then gate3 = param_1 and gate1 = (all devices idle); the empty/all-idle
    /// path writes 1 (<c>0x9EBB8C</c>, <c>0x9EBBA8</c>). The sem post when param_1 != 0 is the caller's
    /// thread seam. Returns 1 on the empty/all-idle path (<c>0x9EBB8C mov r4,#1</c>, <c>0x9EBBC0 mov r0,r7</c>).
    /// </summary>
    public int AdvanceFrame(int param)
    {
        bool allIdle = _devices.All(d => d.AdvanceState == 0);
        foreach (var device in _devices)
            if (device.AdvanceState != 0)
                device.AdvanceState = 0;                // create PBI/GetJSON and reset +0x88 (caller seam)
        Gate3 = (byte)param;
        Gate1 = (byte)(allIdle ? 1 : 0);                // 0x9EBB8C/0x9EBBA8: all idle -> 1
        return 1;
    }

    /// <summary>
    /// G5 <c>0x9EC418(param_1, param_2, param_3)</c>, transliterated from the instructions
    /// <c>0x9EC418..0x9EC583</c> (verifier pass 2). The exact flow:
    /// <list type="number">
    /// <item>if the <c>(param_1, param_2)</c> pair equals <c>(gate2, param_2)</c>-equivalent state, return 3
    /// (<c>0x9EC434</c>/<c>0x9EC438</c>/<c>0x9EC514..0x9EC528</c>);</item>
    /// <item><c>max = max(param_3, countdown)</c> (<c>0x9EC44C..0x9EC458</c>); if <c>21 &lt; max</c>
    /// (<c>0x9EC45C</c>/<c>0x9EC460</c>): <c>countdown = max - 21</c> (<c>0x9EC530..0x9EC53C</c>), call
    /// <c>FUN_009b08f4(manager, param_1, param_2)</c> (<c>0x9EC544..0x9EC54C</c>); then if
    /// <c>gate2 == 0 || gate3 != 0</c> return 1 (<c>0x9EC550..0x9EC558</c>, <c>0x9EC564..0x9EC568</c>,
    /// <c>0x9EC57C</c>) — <b>without writing gate2/gate3 from param_1/param_2</b>; otherwise
    /// <c>gate3 = 1; gate2 = 1</c> (<c>0x9EC56C/0x9EC570/0x9EC574</c>) and return
    /// <c>AdvanceFrame(old gate2)</c> (<c>0x9EC578</c> beq <c>0x9EC490</c>; r0 is the gate2 loaded at
    /// <c>0x9EC550</c>);</item>
    /// <item>else <c>countdown = 0</c> (<c>0x9EC464..0x9EC46C</c>); if <c>(param_1 | gate2) == 0</c>
    /// (<c>0x9EC464</c> orrs, <c>0x9EC470</c>) set <c>gate2 = 0; gate3 = 1</c> (<c>0x9EC49C..0x9EC4A8</c>);
    /// otherwise <c>gate2 = param_1</c> (<c>0x9EC47C</c>), <c>gate3 = param_2</c> (<c>0x9EC488</c>), and when
    /// <c>param_1 != 0</c> return <c>AdvanceFrame(param_2)</c> (<c>0x9EC484</c>, <c>0x9EC48C</c>,
    /// <c>0x9EC490/0x9EC494</c>);</item>
    /// <item>the inline <c>[node+0x88]</c> walk (<c>0x9EC4AC..0x9EC508</c>): for each node, skip states 1 and
    /// 2 (<c>0x9EC4C4..0x9EC4D0</c>), else set the state to <b>2</b> (<c>0x9EC4BC/0x9EC4D4</c>) and, when
    /// <c>FUN_0099dc54(param_1) != 0</c> (<c>0x9EC4D8..0x9EC4E0</c>), call <c>FUN_00a40924(manager+0x54)</c>
    /// (<c>0x9EC4E4..0x9EC4F4</c>);</item>
    /// <item><c>AdvanceDevices(1)</c> (<c>0x9EC504/0x9EC508</c>) and return 1 (<c>0x9EC50C/0x9EC510</c>).</item>
    /// </list>
    /// </summary>
    /// <param name="apply">The <c>FUN_009b08f4</c> command hook, called with <c>(param_1, param_2)</c>; caller seam.</param>
    /// <param name="deviceStarted">The <c>FUN_0099dc54</c> "did the device start?" predicate; caller seam.</param>
    /// <param name="semPost">The <c>FUN_00a40924</c> semaphore post; caller seam.</param>
    public int SetOutputDevice(
        byte param1, byte param2, byte param3,
        Action<byte, byte>? apply = null,
        Func<byte, bool>? deviceStarted = null,
        Action? semPost = null)
    {
        if (param1 == Gate2 && Gate3 == param2) return 3;          // 0x9EC434/0x9EC438/0x9EC514..0x9EC528

        int max = param3 < Countdown ? Countdown : param3;         // 0x9EC44C..0x9EC458
        if (21 < max)                                              // 0x9EC45C/0x9EC460
        {
            Countdown = max - 21;                                  // 0x9EC530/0x9EC534/0x9EC53C
            apply?.Invoke(param1, param2);                         // 0x9EC544..0x9EC54C FUN_009b08f4
            if (Gate2 == 0 || Gate3 != 0) return 1;                // 0x9EC550..0x9EC558, 0x9EC564..0x9EC568, 0x9EC57C
            byte oldGate2 = Gate2;                                 // 0x9EC550
            Gate3 = 1;                                             // 0x9EC56C/0x9EC570
            Gate2 = 1;                                             // 0x9EC574
            return AdvanceFrame(oldGate2);                         // 0x9EC578 -> 0x9EC490
        }

        Countdown = 0;                                             // 0x9EC464/0x9EC468/0x9EC46C
        if ((param1 | Gate2) == 0)                                 // 0x9EC464 orrs; 0x9EC470
        {
            Gate2 = 0;                                             // 0x9EC49C
            Gate3 = 1;                                             // 0x9EC4A0/0x9EC4A8
        }
        else
        {
            Gate2 = param1;                                        // 0x9EC47C
            Gate3 = param2;                                        // 0x9EC488
            if (param1 != 0) return AdvanceFrame(param2);          // 0x9EC484 movne r0,ip; 0x9EC48C; 0x9EC490/0x9EC494
        }

        // inline [node+0x88] walk (0x9EC4AC..0x9EC508)
        foreach (var device in _devices)
        {
            if ((uint)(device.AdvanceState - 1) > 1)               // 0x9EC4C4..0x9EC4D0: skip states 1 and 2
            {
                device.AdvanceState = 2;                           // 0x9EC4BC r6=2; 0x9EC4D4
                if (deviceStarted?.Invoke(param1) == true)         // 0x9EC4D8..0x9EC4E0 FUN_0099dc54
                    semPost?.Invoke();                             // 0x9EC4E4..0x9EC4F4 FUN_00a40924
            }
        }
        AdvanceDevices(1);                                         // 0x9EC504/0x9EC508
        return 1;                                                  // 0x9EC50C/0x9EC510
    }
}

/// <summary>
/// The Android-JNI audio-route poll (M6-022 P1..P7, C13). This is a per-64-tick poll, not a Wwise
/// pre-update: it gates on the <c>JavaVM*</c> <c>[0x108DA84]</c>, the app Context <c>[0x108DF9C]</c> and
/// <c>([0x108D870]+0x4C &amp; 0x3f)==0</c>; it resolves <c>android/media/AudioManager</c>, calls
/// <c>isBluetoothA2dpOn</c>/<c>isBluetoothScoOn</c>, ORs the results into <c>0x108DF98</c>, and on a change
/// calls the output-device notification <c>0x9EA66C</c>.
///
/// <para><b>Caller seam.</b> This stack has no phone JVM, so the JNI calls (<c>GetEnv</c>/
/// <c>AttachCurrentThread</c>/<c>FindClass</c>/<c>NewStringUTF</c>/<c>GetMethodID</c>/<c>CallObjectMethodV</c>/
/// <c>CallBooleanMethodV</c>) are not invented. The recovered decision structure is modelled and the
/// bluetooth-active answer is supplied by the caller; the poll does nothing when the caller reports no JVM
/// or context.</para>
/// </summary>
public interface IWwiseJniAudioRoute
{
    /// <summary>P1/P7: the <c>JavaVM*</c> <c>[0x108DA84]</c> is present.</summary>
    bool HasJavaVm { get; }

    /// <summary>P1/P7: the app Context <c>[0x108DF9C]</c> is present.</summary>
    bool HasContext { get; }

    /// <summary>
    /// P6: the OR of <c>isBluetoothA2dpOn</c> and <c>isBluetoothScoOn</c> (<c>0xA57EE4..0xA57EFC</c>). This
    /// is the caller's substitution for the phone's AudioManager query; once the gates pass the source always
    /// computes it, so there is no null/unavailable path (verifier finding 7).
    /// </summary>
    bool BluetoothActive { get; }
}

/// <summary>
/// M6-022 P1..P7: the poll's decision structure. It runs once per 64 manager ticks and calls the route
/// notification on a bluetooth-active change.
/// </summary>
public sealed class WwiseJniAudioRoutePoll
{
    private readonly IWwiseJniAudioRoute _route;
    private readonly Action _onRouteChanged;

    /// <param name="route">The JNI/phone route seam (see <see cref="IWwiseJniAudioRoute"/>).</param>
    /// <param name="onRouteChanged">P6: <c>0x9EA66C</c>, the output-device notification on a change.</param>
    public WwiseJniAudioRoutePoll(IWwiseJniAudioRoute route, Action onRouteChanged)
    {
        _route = route ?? throw new ArgumentNullException(nameof(route));
        _onRouteChanged = onRouteChanged ?? throw new ArgumentNullException(nameof(onRouteChanged));
    }

    /// <summary>P7: the cached bluetooth-active byte <c>0x108DF98</c>.</summary>
    public bool BluetoothActive { get; private set; }

    /// <summary>How many times the poll actually ran (passed the gates and read the route).</summary>
    public int Polls { get; private set; }

    /// <summary>
    /// One tick of the poll (P1): gate on the JVM, the Context and <c>(tick &amp; 0x3f)==0</c>; then read
    /// the route and notify on a change.
    /// </summary>
    public void Poll(long managerTick)
    {
        if (!_route.HasJavaVm || !_route.HasContext) return;        // P1
        if ((managerTick & 0x3f) != 0) return;                       // P1: once per 64 ticks
        Polls++;

        bool active = _route.BluetoothActive;                        // P2..P6, 0xA57EE4..0xA57EFC
        if (active == BluetoothActive) return;
        BluetoothActive = active;                                    // P6: 0xA57F04
        _onRouteChanged();                                           // P6: 0xA57F38 -> 0x9EA66C
    }
}

/// <summary>
/// One voice-pass / bus-pass callback pair (M6-022 V5..V20, C12 voice-callees Q4..Q9). The recovered order
/// is fixed by the engine; each callback's body is the record's, so this is a caller seam rather than a
/// re-implementation here.
/// </summary>
public interface IWwiseVoiceBusPass
{
    /// <summary>
    /// V5 pre-pass (<c>0x9D3CC0</c>, <c>0xA43D24</c>, <c>0xA39564</c>) then the voice list (V6..V16).
    /// </summary>
    void VoicePass();

    /// <summary>V17 <c>0xA44C18(arg)</c>: the bus pass, last-to-first, then idle removal <c>0xA43F64</c>.</summary>
    void BusPass(int arg);

    /// <summary>V21 <c>0xA38420</c>: the deferred PBI-notification flush.</summary>
    void FlushPbiNotifications();
}

/// <summary>
/// One Perform group member (M6-022 V25..V28, C12 bus-group Q4..Q7). The class names are UNKNOWN (no
/// RTTI/symbols); the engine calls them in the V29 order and the caller supplies each behaviour.
/// </summary>
public interface IWwisePerformGroupMember
{
    /// <summary>The member's tick entry, with the engine tick (Perform passes <c>tick+1</c> to the first two).</summary>
    void Tick(long tick);
}

/// <summary>
/// M6-022: the live voice and bus engine — the <c>0xA57FF8</c> wrapper around the <c>0xA44D4C</c> render
/// body, its pre-loop and throttle, the bus-pass gate, the voice/bus passes, the four Perform group members,
/// the PBI flush, the output-device state/gates and the JNI audio-route poll.
///
/// <para><b>What is settled and modelled.</b> The output-device state and the three gate bytes with their
/// writers (G1..G10), the device advance <c>0x9EBE6C</c> and the SetOutputDevice command <c>0x9EC418</c>
/// (D1..D4, G5), the render-body pre-loop (V3/N1), the throttle (N3), the bus-pass argument
/// <c>(gate2==0)?1:gate3</c> (V4/G10), the JNI poll decision structure (P1..P7) and the Perform call order
/// (V29).</para>
///
/// <para><b>Caller seams</b> (the inventory classifies these RECOVERABLE_GAP/HARDWARE_ONLY/UNKNOWN; the
/// recovered path is modelled and the value/object is an input):
/// <list type="bullet">
/// <item>the output-device node's <c>[node+0x70]</c> sub-object identity and the device sink frame count /
/// OpenSL pacing (UNKNOWN / HARDWARE_ONLY) — <see cref="IWwiseOutputDevice"/>;</item>
/// <item>the JNI route calls (no phone) — <see cref="IWwiseJniAudioRoute"/>;</item>
/// <item>the per-voice DSP chain and bus pass bodies (the record's own rows; modelled as
/// <see cref="IWwiseVoiceBusPass"/> so the engine's order is fixed and the bodies live with their owner);
/// </item>
/// <item>the four group members' class identities (UNKNOWN) — <see cref="IWwisePerformGroupMember"/>.</item>
/// </list>
/// The insert-FX slot object identity (V12/V18b) is RECOVERABLE_GAP; the FX factory is a caller seam.</para>
/// </summary>
public sealed class WwiseVoiceEngine : IWwiseFrameRender
{
    private readonly WwiseOutputDeviceState _deviceState;
    private readonly IWwiseVoiceBusPass _pass;
    private readonly IWwisePerformGroupMember[] _group;
    private readonly WwiseJniAudioRoutePoll? _routePoll;

    // G9/N3: the render body's last-tick throttle 0x108DA9C (caller-owned here).
    private long _lastThrottleTick;

    /// <param name="deviceState">The output-device state (G1..G10).</param>
    /// <param name="pass">The voice/bus pass bodies (V5..V21).</param>
    /// <param name="group">
    /// The four Perform group members (V25..V28). The engine calls them in the V29 order; pass an empty
    /// array to run without them.
    /// </param>
    /// <param name="routePoll">The JNI audio-route poll (P1..P7), or null when there is no phone route.</param>
    public WwiseVoiceEngine(
        WwiseOutputDeviceState deviceState,
        IWwiseVoiceBusPass pass,
        IReadOnlyList<IWwisePerformGroupMember>? group = null,
        WwiseJniAudioRoutePoll? routePoll = null)
    {
        _deviceState = deviceState ?? throw new ArgumentNullException(nameof(deviceState));
        _pass = pass ?? throw new ArgumentNullException(nameof(pass));
        _group = group is null ? Array.Empty<IWwisePerformGroupMember>() : group.ToArray();
        _routePoll = routePoll;
    }

    /// <summary>The output-device state and gates this engine drives.</summary>
    public WwiseOutputDeviceState DeviceState => _deviceState;

    /// <summary>N3: how many render bodies the throttle skipped.</summary>
    public int ThrottledFrames { get; private set; }

    /// <summary>V29: how many Perform ticks the engine has run.</summary>
    public long PerformTick { get; private set; }

    /// <summary>
    /// M6-017 render seam: the render group before LEngine, V29's
    /// <c>0xA36AC4(tick+1)</c> → <c>0x9FF308(tick+1)</c> → <c>0x9D3C98()</c> → <c>0x9E6D2C()</c>.
    /// </summary>
    public void RenderBuses()
    {
        long t = PerformTick + 1;
        if (_group.Length > 0) _group[0].Tick(t);                       // V25 0xA36AC4
        if (_group.Length > 1) _group[1].Tick(t);                       // V26 0x9FF308
        if (_group.Length > 2) _group[2].Tick(t);                       // V27 0x9D3C98
        if (_group.Length > 3) _group[3].Tick(t);                       // V28 0x9E6D2C
    }

    /// <summary>
    /// V1/V3/V4/V5..V20: the <c>0xA57FF8</c> wrapper — the <c>0xA57D64</c> poll, then the
    /// <c>0xA44D4C</c> render body. The body's pre-loop and throttle run, then the voice pass
    /// (<c>0xA44948</c>) and the bus pass (<c>0xA44C18</c>) with the V4 gate argument.
    /// </summary>
    public void RunLEngine()
    {
        _routePoll?.Poll(_deviceState.ManagerTick);                     // V1: 0xA57D64
        RenderBody();
    }

    /// <summary>
    /// The <c>0xA44D4C</c> render body (V3/N1, N3, V4): pre-loop, throttle, then the voice pass and the
    /// bus pass. N3's throttle skips when <c>0 &lt; tick-last &lt;= 8</c>; the bus-pass argument is V4's
    /// <c>(gate2==0)?1:gate3</c>.
    /// </summary>
    public void RenderBody()
    {
        _deviceState.PreLoop();                                         // V3/N1

        long tick = _deviceState.ManagerTick;
        long last = _lastThrottleTick;
        if (last == 0)
        {
            _lastThrottleTick = tick;                                   // N3: store when last == 0
        }
        else
        {
            long delta = tick - last;
            if (delta > 0 && delta <= 8)
            {
                ThrottledFrames++;                                      // N3: skip
                return;
            }
            _lastThrottleTick = tick;                                   // N3: else store
        }

        _pass.VoicePass();                                              // V4: 0xA44948
        _pass.BusPass(_deviceState.BusPassArg);                         // V4: 0xA44C18
    }

    /// <summary>M6-017 render seam: V21 <c>0xA38420</c>, the PBI-notification flush.</summary>
    public void FlushPbiNotifications() => _pass.FlushPbiNotifications();

    /// <summary>
    /// V29 Perform: the render group, then the <c>0xA57FF8</c> wrapper, then the PBI flush. The
    /// <c>0x99DA54(0x10)</c> notification and the <c>tick++</c> are the caller's (the tick lives with
    /// <see cref="WwiseEventRuntime"/>).
    /// </summary>
    public void Perform()
    {
        RenderBuses();
        RunLEngine();
        FlushPbiNotifications();
        PerformTick++;
    }
}