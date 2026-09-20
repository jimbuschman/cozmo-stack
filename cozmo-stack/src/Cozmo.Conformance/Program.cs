using System.Globalization;
using System.Net;
using System.Text;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Cozmo.Transport;

// cozmo-conformance: encode/decode/compare/replay Cozmo transport frames and run the hardware smoke test.
// Nothing here depends on the Android app, libcozmoEngine or Python.

return args.Length == 0 ? Usage() : args[0] switch
{
    "decode" => Decode(args.Skip(1)),
    "diff" => Diff(args),
    "fixtures" => Fixtures(),
    "catalog" => Catalog(args),
    "pcap" => PcapCmd(args),
    "replay" => Replay(args),
    "connect" => Connect(args).GetAwaiter().GetResult(),
    "fakerobot" => FakeRobot(args),
    "probe" => Probe.Run(args).GetAwaiter().GetResult(),
    "camera" => Devices.Camera(args).GetAwaiter().GetResult(),
    "face" => Devices.Face(args).GetAwaiter().GetResult(),
    "tone" => Devices.Tone(args).GetAwaiter().GetResult(),
    "drive" => Control.Drive(args).GetAwaiter().GetResult(),
    "lights" => Control.Lights(args).GetAwaiter().GetResult(),
    "sensors" => Control.Sensors(args).GetAwaiter().GetResult(),
    "cubes" => Control.Cubes(args).GetAwaiter().GetResult(),
    "animlist" => Anim.List(args),
    "animdump" => AnimDump.Run(args),
    "anim" => Anim.Play(args).GetAwaiter().GetResult(),
    "face-expressions" => Anim.Face(args).GetAwaiter().GetResult(),
    "wwise" => WwiseTool.Run(args),
    "triggers" => TriggersTool.Run(args),
    "behavior" => BehaviorTool.Run(args).GetAwaiter().GetResult(),
    "sing" => SingTool.Run(args).GetAwaiter().GetResult(),
    "offtreads" => ReactionsTool.OffTreads(args).GetAwaiter().GetResult(),
    "reactions" => ReactionsTool.Run(args).GetAwaiter().GetResult(),
    _ => Usage(),
};

/// <summary>
/// A minimal stand-in for the robot firmware's transport behaviour, for loopback testing of the socket/thread path
/// without hardware: accepts ConnectionRequest, echoes pings, answers GetManufacturingInfo/SyncTime, streams RobotState
/// at ~33 Hz as unreliable messages batched with reliable events in MultipleMixed frames, acks with the header field.
/// It is NOT a robot emulator; its message contents are synthetic.
/// </summary>
static int FakeRobot(string[] a)
{
    int port = 5551; int seconds = 60;
    for (int i = 1; i < a.Length; i++) { if (a[i] == "--port") port = int.Parse(a[++i]); else if (a[i] == "--seconds") seconds = int.Parse(a[++i]); }
    using var sock = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Loopback, port));
    sock.Client.ReceiveTimeout = 20;
    Console.WriteLine($"fakerobot listening on 127.0.0.1:{port} for {seconds}s");
    IPEndPoint? peer = null; ushort nextOut = 1, lastInAcked = 0, nextIn = 1; uint ts = 0; float head = 0f, targetHead = 0f;
    var pendingReliable = new List<(ushort seq, byte[] payload)>();
    var end = DateTime.UtcNow.AddSeconds(seconds); var lastState = DateTime.UtcNow; bool connected = false;
    byte[] Wrap(RobotMessage m) => m.ToBytes();
    void SendFrame(Frame f) { if (peer is null) return; var raw = FrameCodec.Encode(f); sock.Send(raw, raw.Length, peer); }
    ushort Reliable(byte[] payload) { var s = nextOut; nextOut = SequenceId.Next(s); pendingReliable.Add((s, payload)); return s; }
    while (DateTime.UtcNow < end)
    {
        IPEndPoint from = new(IPAddress.Any, 0); byte[]? raw = null;
        try { raw = sock.Receive(ref from); } catch (System.Net.Sockets.SocketException) { }
        if (raw is not null && FrameCodec.TryDecode(raw, out var f, out _))
        {
            var h = f!.Header;
            // header ack from the engine
            if (h.Ack != 0 && pendingReliable.Count > 0)
            {
                ushort first = pendingReliable[0].seq;
                pendingReliable.RemoveAll(p => SequenceId.InRange(p.seq, first, h.Ack));
            }
            foreach (var m in f.Messages)
            {
                if (m.IsReliable) { if (m.Seq != nextIn) continue; nextIn = SequenceId.Next(nextIn); lastInAcked = m.Seq; }
                switch (m.Type)
                {
                    case ReliableMessageType.ConnectionRequest:
                        peer = from; connected = true; nextOut = 1; pendingReliable.Clear();
                        Console.WriteLine($"  connection request from {from}");
                        var resp = new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), Reliable(Array.Empty<byte>()));
                        SendFrame(Frame.Single(resp, lastInAcked));
                        var avail = Wrap(new RobotAvailable { SerialNumberHead = 0x0BADF00D, HwVersion = 5 });
                        var fw = Wrap(new FirmwareVersion { RobotId = 0x4D9D, Signature = Encoding.UTF8.GetBytes("{\"version\": 2381, \"build\": \"FAKE\", \"messageEngineToRobotHash\": \"9e4a965ace4e09d86997b87ba14235d5\", \"messageRobotToEngineHash\": \"a259247f16231db440957215baba12ab\"}") });
                        SendFrame(Frame.Multiple(new[] { SubMessage.Data(avail, true, Reliable(avail)), SubMessage.Data(fw, true, Reliable(fw)) }, lastInAcked));
                        break;
                    case ReliableMessageType.DisconnectRequest: Console.WriteLine("  disconnect request"); connected = false; peer = null; break;
                    case ReliableMessageType.Ping: SendFrame(Frame.Single(new SubMessage(ReliableMessageType.Ping, m.Payload), lastInAcked)); break;
                    case ReliableMessageType.SingleReliableMessage:
                    case ReliableMessageType.SingleUnreliableMessage:
                        var msg = RobotMessage.Parse(m.Payload);
                        Console.WriteLine($"  <- {msg}");
                        byte[]? reply = msg switch
                        {
                            GetManufacturingInfo => Wrap(new ManufacturingID { SerialNumber = 0x0BADF00D, BodyHwVersion = 5, BodyColor = 3 }),
                            SyncTime => Wrap(new SyncTimeAck()),
                            SetHeadAngle sh => Then(() => targetHead = sh.AngleRad, Wrap(new MotorActionAck { ActionId = sh.ActionId })),
                            _ => null,
                        };
                        if (reply is not null) SendFrame(Frame.Single(SubMessage.Data(reply, true, Reliable(reply)), lastInAcked));
                        break;
                }
            }
        }
        if (connected && peer is not null && (DateTime.UtcNow - lastState).TotalMilliseconds >= 33)
        {
            lastState = DateTime.UtcNow; ts += 33; head += Math.Clamp(targetHead - head, -0.05f, 0.05f);
            var st = Wrap(new RobotState { Timestamp = ts, PoseOriginId = 1, HeadAngle = head, LiftAngle = RobotState.LiftAngleRadFromHeight(32f), BatteryVoltage = 3.9f, Status = (uint)(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos) });
            var subs = new List<SubMessage> { SubMessage.Data(st, false) };
            foreach (var p in pendingReliable) subs.Add(SubMessage.Data(p.payload, true, p.seq)); // resend unacked reliable with the state
            SendFrame(Frame.Multiple(subs, lastInAcked));
        }
    }
    return 0;
    byte[] Then(Action act, byte[] r) { act(); return r; }
}

static int Usage()
{
    Console.WriteLine("""
        cozmo-conformance <command> [args]

          decode <hex|python-bytes>...      parse one or more raw UDP frames (14-byte header + body) and print header, sub-messages, CLAD messages
          diff <hexA> <hexB>                 byte-by-byte comparison of two frames with field hints
          fixtures                           decode + re-encode PyCozmo's captured test frames (round-trip must be byte-identical)
          catalog [tag|name]                 print the official 161-message catalog (or one entry)
          pcap <file.pcap> [--raw]           decode every UDP datagram to/from ports 5551/5552 in a capture
          replay <file.pcap|hexfile>         feed robot->engine frames from a capture through our receive path and report what the connection state machine did
          connect <robot-ip> [--port 5551] [--seconds 20] [--head <rad>] [--led] [--headlight] [--log <file>] [--origin]
                                             (a frame log is always written; default cozmo-frames-<timestamp>.log in the current directory, full path printed)
                                             hardware smoke test: connect, handshake, telemetry, one harmless command, disconnect
          fakerobot [--port 5551] [--seconds 60]  loopback stand-in for the robot transport (127.0.0.1) for testing the socket path without hardware
          probe <robot-ip> [--include-motion] [--include-state] [--only <step>] [--out results.json]
                                             subsystem-by-subsystem protocol verification on a real robot: sends only
                                             read-only/safe-visible messages by default, decodes and re-encodes every
                                             reply, and writes a per-message hardware-verification report

          camera <robot-ip> [--count 10] [--out <dir>] [--color] [--acceptance [file.json]] [--log <file>]
                                             hardware acceptance for the camera: stream frames and save them as JPEG files
          face <robot-ip> [--pattern test|eyes|full|blank] [--file <ascii-art>] [--seconds 5]
                          [--acceptance [file.json]] [--log <file>]
                                             hardware acceptance for the OLED: draw a known image and hold it
          tone <robot-ip> [--sound steady|beeps|sweep] [--codec anki|mulaw|pcm8u|pcm8s] [--hz 440]
                          [--seconds 2] [--amplitude 0.5] [--acceptance [file.json]]
                          [--volume <n>] [--save <file.wav>] [--unreliable] [--in-flight <n>] [--log <file>]
                                             hardware acceptance for the speaker: play a generated sine tone.
                                             All three write an acceptance record: what was measured here is
                                             separated from what a person still has to see or hear, because
                                             this tool can do neither
                                             --save writes exactly what is sent as a .wav, to check the
                                             encoding locally; --unreliable and --in-flight vary how it is
                                             delivered, to separate encoding faults from delivery faults;
                                             --sound beeps plays a countable number of separate beeps, which
                                             tests continuity without having to judge tone quality;
                                             --codec anki (the default) is the companding transcribed from
                                             the engine itself; the others are for comparison only

          drive <robot-ip> [--allow-drive] [--speed 40] [--drive-seconds 1] [--acceptance [file.json]]
                                             hardware acceptance for motion: head and lift, confirmed by the
                                             robot's own action acknowledgement, then a short forward and
                                             back only if --allow-drive is given, then a checked stop
          lights <robot-ip> [--seconds 1.5] [--acceptance [file.json]]
                                             hardware acceptance for the backpack LEDs and the infrared
                                             headlight. The robot reports nothing about either, so the
                                             verdict is entirely yours
          sensors <robot-ip> [--seconds 8] [--acceptance [file.json]]
                                             hardware acceptance for battery, charger, cliff and IMU: reads
                                             what the robot reports about itself and prints it once a second
          cubes <robot-ip> [--seconds 15] [--acceptance [file.json]]
                                             hardware acceptance for cubes: turns discovery on and reports
                                             every cube heard, with connection state and telemetry

          behavior <robot-ip> --obb <dir> [--seconds 60] [--no-idle] [--no-react] [--allow-motion]
                                             hardware acceptance for the reactive and idle layers: reacts
                                             to being picked up, cliffs and the charger, and keeps an idle
                                             robot alive. Prints every decision, including suppressed ones

          triggers <obb-dir> [--trigger <name>] [--mood <mood>] [--seed N] [--limit 20]
                                             resolve Anki's own animation triggers through the shipped
                                             AnimationTriggerMap to a group and a selected animation,
                                             showing every step. No robot involved

          wwise <sound-dir> [--event <id-or-name>] [--clip <name> --assets <dir>] [--coverage] [--limit 20]
                                             resolve Cozmo's own audio events through the shipped Wwise banks
                                             to the media files they play, reporting codec and duration.
                                             --coverage reports the whole library. No robot involved
          wwise <sound-dir> --hierarchy      check that every hierarchy object in the banks reads exactly
          wwise <sound-dir> --music <event> [--switch Group=State]... [--midi]
                                             show how a music event plays: switch tree, playlist, segments,
                                             clips and, with --midi, the notes
          wwise <sound-dir> --render <event> [--switch Group=State]... [--wav <file>] [--seed N]
                                             render a music event (a Cozmo_Sings song) to PCM offline and
                                             report notes played, silent notes, clipping; --wav saves it
          wwise <sound-dir> --validate-music [--obb <dir>] [--seed N]
                                             render every shipped song (the 39 behaviours when --obb is
                                             given) and every other music event, and report each
          sing <robot-ip> --obb <dir> (--behavior <Singing_X> | --group <G> --switch <S>) [--seconds 45]
                          [--acceptance [file.json]]
                                             hardware acceptance for M9: runs the shipped Singing behaviour
                                             on the robot (switch, get-in, song, get-out) and records what
                                             was rendered; whether it sang is the human check
          offtreads <robot-ip> [--seconds 60] [--acceptance [file]]
                                             M10: print every off-treads state change and unexpected-movement report the engine's
                                             classifier and detector derive from the streamed state; --replay <frame-log> runs them offline
          reactions <robot-ip> --obb <dir> [--seconds 120] [--acceptance [file]]
                                             M10 acceptance: the derived-state reactions (on back, face, side, slope, shaken, pushed,
                                             calibration, frustration) under the behaviour manager; every trigger and step printed

          animlist <assets-dir> [filter]     decode Cozmo's own animation assets and list what is in them,
                                             with no robot involved
          animdump <assets-dir> <clip-name> [--out <file>]
                                             print every decoded keyframe of one clip in timestamp order,
                                             which ones the executor acts on and which it ignores, and every
                                             message the real player would send, taken from the transport.
                                             No robot involved
          anim <robot-ip> (--assets <dir> (--name <clip> | --group <group>) | --arc)
                          [--audio <eventId>=<file.wav>]... [--arc-radius 60] [--arc-speed 30] [--arc-seconds 1]
                                             play an animation on the robot, on the single 30 Hz timeline
                                             that also drives the face. --audio maps a sound to an audio
                                             event id, repeatable, so animation audio can be heard without a
                                             Wwise decoder. --arc plays a synthetic clip that arcs one way
                                             and back, for testing body motion; stop-on-cliff is enabled
                                             before any body motion either way
          face-expressions <robot-ip> [--seconds 2] | face-expressions --offline
                                             show every built-in procedural expression, printing the art it
                                             sent so the robot's face can be compared against it;
                                             --offline prints the art only, with no robot
        """);
    return 1;
}

static int Decode(IEnumerable<string> inputs)
{
    int rc = 0;
    foreach (var s in inputs)
    {
        var raw = Hex.Parse(s);
        Console.WriteLine($"--- {raw.Length} bytes: {Hex.Dump(raw, 64)}");
        if (!FrameCodec.TryDecode(raw, out var f, out var err)) { Console.WriteLine($"  !! {err}"); rc = 2; continue; }
        PrintFrame(f!, "  ");
        var re = FrameCodec.Encode(f!);
        if (!re.AsSpan().SequenceEqual(raw)) { Console.WriteLine("  !! re-encode differs:"); foreach (var d in Hex.Diff(raw, re, "input", "re-encoded")) Console.WriteLine("     " + d); rc = 3; }
        else Console.WriteLine("  re-encode: identical");
    }
    return rc;
}

static void PrintFrame(Frame f, string ind)
{
    Console.WriteLine($"{ind}{f.Type}(0x{(byte)f.Type:x2}) seqMin={f.SeqMin} seqMax={f.SeqMax} ack={f.Ack} reliable={f.Header.IsReliable} messages={f.Messages.Count}");
    foreach (var m in f.Messages)
    {
        string desc = m.Type switch
        {
            ReliableMessageType.Ping => PingPayload.TryParse(m.Payload, out var p) ? p.ToString() : "malformed ping",
            ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage =>
                m.Payload.Length == 0 ? "(empty)" : RobotMessage.Parse(m.Payload).ToString(),
            ReliableMessageType.MultiPartMessage => m.Payload.Length >= 2 ? $"part {m.Payload[0]}/{m.Payload[1]} ({m.Payload.Length - 2} bytes)" : "malformed multipart",
            _ => m.Payload.Length == 0 ? "" : Hex.Dump(m.Payload, 24),
        };
        Console.WriteLine($"{ind}  [{m.Type}{(m.IsReliable ? $" seq={m.Seq}" : "")} {m.Payload.Length}B] {desc}");
    }
}

static int Diff(string[] a)
{
    if (a.Length < 3) return Usage();
    var x = Hex.Parse(a[1]); var y = Hex.Parse(a[2]);
    var d = Hex.Diff(x, y);
    if (d.Count == 0) { Console.WriteLine("identical"); return 0; }
    foreach (var l in d) Console.WriteLine(l);
    return 1;
}

static int Fixtures()
{
    // Captured on real hardware by the PyCozmo project (pycozmo/tests/test_frame.py). Wire values, not PyCozmo's -1 values.
    var cases = new (string name, string bytes, ReliableMessageType type, int seqMin, int seqMax, int ack, int count)[]
    {
        ("engine type7 4 reliable msgs (first=2717 last=2720 ack=143)",
         "b'COZ\\x03RE\\x01\\x07\\x9d\\n\\xa0\\n\\x8f\\x00\\x04\\x01\\x00\\x8f\\x04\\x1d\\x00\\x97\\x1a\\x00\\x15\\xb0\\xaa\\x9c\\xac\\xb2@\\xa8\\xba^\\xac\\xb2@\\x02\\xb4\\xa2\\xa0\\xb0\\xaa@\\xac\\xb2`\\xb0\\xaa\\x1b\\x04 \\x00\\x03\\x1f\\x80\\x1f\\x80\\t\\x00\\x00\\x00\\x00\\x00\\x1f\\x80\\x1f\\x80\\t\\x00\\x00\\x00\\x00\\x00\\x1f\\x80\\x1f\\x80\\t\\x00\\x00\\x00\\x00\\x00\\x00\\x04\\x16\\x00\\x11\\x1f\\x80\\x1f\\x80\\t\\x00\\x00\\x00\\x00\\x00\\x1f\\x80\\x1f\\x80\\t\\x00\\x00\\x00\\x00\\x00\\x00'",
         ReliableMessageType.MultipleReliableMessages, 2717, 2720, 143, 4),
        ("robot type9 ping-echo + AnimationState v2214 (unreliable only)",
         "b'COZ\\x03RE\\x01\\t\\x00\\x00\\x00\\x00\\x13\\x00\\x0b\\x11\\x00\\x00\\x00\\x00\\x00\\x00\\x00\\x00\\x00\\x01\\x00\\x00\\x00\\x00\\x00\\x00\\x00\\x00\\x05\\x0f\\x00\\xf1y\\x8bJO$\\x00\\x00\\x00\\x06\\x00\\x00\\x00\\xff\\x00'",
         ReliableMessageType.MultipleMixedMessages, 0, 0, 19, 2),
    };
    int rc = 0;
    foreach (var c in cases)
    {
        Console.WriteLine($"== {c.name}");
        var raw = Hex.Parse(c.bytes);
        var f = FrameCodec.Decode(raw);
        PrintFrame(f, "   ");
        bool ok = f.Type == c.type && f.SeqMin == c.seqMin && f.SeqMax == c.seqMax && f.Ack == c.ack && f.Messages.Count == c.count;
        var re = FrameCodec.Encode(f);
        bool same = re.AsSpan().SequenceEqual(raw);
        Console.WriteLine($"   header/count check: {(ok ? "PASS" : "FAIL")}   round-trip: {(same ? "PASS" : "FAIL")}");
        if (!ok || !same) rc = 1;
    }
    return rc;
}

static int Catalog(string[] a)
{
    IEnumerable<MessageInfo> rows = MessageCatalog.ById.Values.OrderBy(i => (byte)i.Id);
    if (a.Length > 1)
    {
        string q = a[1];
        rows = q.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && byte.TryParse(q[2..], NumberStyles.HexNumber, null, out var t)
            ? rows.Where(i => (byte)i.Id == t)
            : rows.Where(i => i.Member.Contains(q, StringComparison.OrdinalIgnoreCase) || i.CladType.Contains(q, StringComparison.OrdinalIgnoreCase) || i.PyCozmoName.Contains(q, StringComparison.OrdinalIgnoreCase));
    }
    Console.WriteLine($"{"tag",-5} {"direction",-14} {"member",-30} {"CLAD type",-30} {"size",5} pycozmo");
    foreach (var i in rows)
        Console.WriteLine($"0x{(byte)i.Id:x2}  {i.Direction,-14} {i.Member,-30} {i.CladType,-30} {(i.OfficialSize < 0 ? "var" : i.OfficialSize.ToString()),5} {i.PyCozmoName}");
    return 0;
}

static int PcapCmd(string[] a)
{
    if (a.Length < 2) return Usage();
    bool rawOut = a.Contains("--raw");
    int n = 0, bad = 0;
    foreach (var d in Pcap.Read(a[1]).Where(d => d.SrcPort is 5551 or 5552 || d.DstPort is 5551 or 5552))
    {
        n++;
        Console.WriteLine($"[{d.TimestampSec:F6}] {(d.FromRobot ? "ROBOT->ENGINE" : "ENGINE->ROBOT")} {d.Src}:{d.SrcPort} -> {d.Dst}:{d.DstPort} {d.Payload.Length}B");
        if (rawOut) Console.WriteLine("   " + Hex.Dump(d.Payload));
        if (FrameCodec.TryDecode(d.Payload, out var f, out var err)) PrintFrame(f!, "   "); else { Console.WriteLine($"   !! {err}"); bad++; }
    }
    Console.WriteLine($"{n} datagrams, {bad} undecodable");
    return bad == 0 ? 0 : 1;
}

/// <summary>Replays robot->engine datagrams from a pcap (or a text file with one hex frame per line) through a fresh ReliableTransport receive path.</summary>
static int Replay(string[] a)
{
    if (a.Length < 2) return Usage();
    var frames = new List<byte[]>();
    if (a[1].EndsWith(".pcap", StringComparison.OrdinalIgnoreCase))
        frames.AddRange(Pcap.Read(a[1]).Where(d => d.FromRobot).Select(d => d.Payload));
    else
        frames.AddRange(File.ReadAllLines(a[1]).Where(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith('#')).Select(Hex.Parse));
    var t = ReliableTransport.CreateOffline(new TransportOptions(), new ManualClock());
    var delivered = new List<string>();
    t.DataReceived += d => delivered.Add(RobotMessage.Parse(d).ToString());
    t.Warning += w => Console.WriteLine("  warn: " + w);
    t.OfflineConnect();
    var sent = t.OfflineOutbound;
    int i = 0;
    foreach (var raw in frames)
    {
        i++;
        if (!FrameCodec.TryDecode(raw, out var f, out var err)) { Console.WriteLine($"#{i} undecodable: {err}"); continue; }
        int before = delivered.Count;
        t.ProcessIncoming(raw);
        var c = t.Connection!;
        Console.WriteLine($"#{i} {f!.Type} seq {f.SeqMin}..{f.SeqMax} ack {f.Ack} -> delivered {delivered.Count - before}, nextIn={c.NextInSeq} lastInAcked={c.LastInAcked} dupsDropped={c.DuplicateReliableDropped} state={t.State}");
        for (int k = before; k < delivered.Count; k++) Console.WriteLine("     " + delivered[k]);
    }
    Console.WriteLine($"{frames.Count} frames replayed, {delivered.Count} messages delivered, {sent.Count} frames our side would have sent");
    return 0;
}

static async Task<int> Connect(string[] a)
{
    if (a.Length < 2 || !IPAddress.TryParse(a[1], out var ip)) return Usage();
    int port = 5551, seconds = 20; float? head = null; bool led = a.Contains("--led"), headlight = a.Contains("--headlight"), origin = a.Contains("--origin");
    string? log = null;
    for (int i = 2; i < a.Length; i++)
    {
        if (a[i] == "--port") port = int.Parse(a[++i]);
        else if (a[i] == "--seconds") seconds = int.Parse(a[++i]);
        else if (a[i] == "--head") head = float.Parse(a[++i], CultureInfo.InvariantCulture);
        else if (a[i] == "--log") log = a[++i];
    }
    log ??= $"cozmo-frames-{DateTime.Now:yyyyMMdd-HHmmss}.log";
    log = Path.GetFullPath(log);
    using var link = new RobotLink();
    StreamWriter? lw = new StreamWriter(log, false, Encoding.UTF8);
    Console.WriteLine($"frame log: {log}");
    link.Transport.FrameTrace += e =>
    {
        if (lw is null) return;
        lock (lw) lw.WriteLine($"{e.Utc:O} {(e.Outbound ? "TX" : "RX")} {Hex.Dump(e.Raw)}{(e.Error is null ? "" : "  !! " + e.Error)}");
    };
    link.Transport.Warning += w => Console.WriteLine("  warn: " + w);
    link.Transport.Disconnected += r => Console.WriteLine($"  disconnected: {r}");
    link.Message += m => { if (m is not RobotState && m is not AnimationState) Console.WriteLine($"  <- {m}"); };

    Console.WriteLine($"connecting to {ip}:{port} (ConnectionRequest, reliable seq 1) ...");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try { await link.ConnectAsync(ip, port, TimeSpan.FromSeconds(5)); }
    catch (Exception e) { Console.WriteLine($"FAIL connect: {e.Message}"); lw?.Dispose(); Console.WriteLine($"frame log written to: {log}"); return 10; }
    Console.WriteLine($"connected in {sw.ElapsedMilliseconds} ms (ConnectionResponse received)");

    await Task.Delay(300);
    Console.WriteLine($"identity: {(link.Available is null ? "RobotAvailable NOT received" : link.Available.ToString())}");
    Console.WriteLine($"firmware: {(link.Firmware is null ? "FirmwareVersion NOT received" : $"v{link.Firmware.Version} e2r={link.Firmware.EngineToRobotHash} r2e={link.Firmware.RobotToEngineHash} build={link.Firmware.Build}")}");

    Console.WriteLine("handshake: GetManufacturingInfo + SyncTime" + (origin ? " + AbsoluteLocalizationUpdate(PyCozmo default)" : ""));
    link.BeginSession(origin);
    await Task.Delay(1000);
    Console.WriteLine($"mfg: {(link.Manufacturing is null ? "MfgId NOT received" : link.Manufacturing.ToString())}   syncTimeAck={link.SyncTimeAcked}   states so far={link.StateCount}");

    float? headBefore = link.LastState?.HeadAngleRad; float headPeak = headBefore ?? 0f;
    link.State += st => { if (Math.Abs(st.HeadAngleRad) > Math.Abs(headPeak)) headPeak = st.HeadAngleRad; };
    if (head is { } h) { Console.WriteLine($"command: SetHeadAngle {h:F3} rad (action 1)"); link.SetHeadAngle(h); }
    if (led) { Console.WriteLine("command: SetBackpackLightsMiddle blue/green/red solid"); link.SetBackpackLights(LightState.Solid(LightState.Rgb(0, 0, 255)), LightState.Solid(LightState.Rgb(0, 255, 0)), LightState.Solid(LightState.Rgb(255, 0, 0))); }
    if (headlight) { Console.WriteLine("command: SetHeadlight on"); link.SetHeadlight(true); }

    var end = DateTime.UtcNow.AddSeconds(seconds);
    int lastCount = 0; var lastPrint = DateTime.UtcNow;
    while (DateTime.UtcNow < end && link.Transport.State == LinkState.Connected)
    {
        await Task.Delay(250);
        if ((DateTime.UtcNow - lastPrint).TotalSeconds >= 2)
        {
            var c = link.Transport.Connection!;
            Console.WriteLine($"  t+{sw.Elapsed.TotalSeconds,5:F1}s states={link.StateCount} (+{link.StateCount - lastCount}) rate={link.StateRateHz:F1}Hz pending={c.PendingCount} nextOut={c.NextOutSeq} nextIn={c.NextInSeq} dups={c.DuplicateReliableDropped} resends={c.ResendFrames} rtt={c.LastPingRoundTripMs:F1}ms pings={c.PingRepliesSeen}  {(link.LastState is null ? "" : $"head={link.LastState.HeadAngleRad:F3} batt={link.LastState.BatteryVoltage:F2}V status=0x{link.LastState.Status:x}")}");
            lastCount = link.StateCount; lastPrint = DateTime.UtcNow;
        }
    }
    if (head is { } h2) { Console.WriteLine("command: SetHeadAngle back to 0"); link.SetHeadAngle(0f, actionId: 2); await Task.Delay(1500); }
    if (led) { link.SetBackpackLights(LightState.Off, LightState.Off, LightState.Off); }
    if (headlight) link.SetHeadlight(false);
    await Task.Delay(200);

    var conn = link.Transport.Connection!;
    Console.WriteLine("---- result ----");
    Console.WriteLine($"connected: {link.Transport.State == LinkState.Connected}");
    Console.WriteLine($"identity received: available={link.Available is not null} firmware={link.Firmware is not null} mfg={link.Manufacturing is not null}");
    Console.WriteLine($"telemetry: {link.StateCount} RobotState at {link.StateRateHz:F1} Hz; {link.MessageCount} messages total; histogram: {string.Join(", ", link.HistogramSnapshot().OrderByDescending(k => k.Value).Select(k => $"{k.Key}={k.Value}"))}");
    Console.WriteLine($"transport: frames sent={conn.FramesSent} resent={conn.ResendFrames} dupsDropped={conn.DuplicateReliableDropped} pending={conn.PendingCount} lastRTT={conn.LastPingRoundTripMs:F1}ms");
    if (head is { } h3) Console.WriteLine($"head angle before={headBefore?.ToString("F3") ?? "n/a"} peak during run={headPeak:F3} (target {h3:F3}) final={link.LastState?.HeadAngleRad:F3}; MotorActionAck count={link.CountOf(RobotMessageId.MotorActionAck)}");
    bool pass = link.Transport.State == LinkState.Connected && link.Available is not null && link.Manufacturing is not null && link.StateCount > 20 && conn.PendingCount < 8;
    Console.WriteLine(pass ? "SMOKE TEST: PASS" : "SMOKE TEST: FAIL");
    link.Disconnect();
    lw?.Dispose();
    Console.WriteLine($"frame log written to: {log}  (send this file plus the console output)");
    return pass ? 0 : 20;
}
