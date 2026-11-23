using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class McpFaceEnrollmentReplayTests(ITestOutputHelper output)
{
    private sealed record Emission(ushort Tag, string Body, uint At, bool NvIdle);

    [Theory]
    [InlineData("COZMO_FACE_REPLAY_BUNDLE")]
    [InlineData("COZMO_MAMBO_REPLAY_BUNDLE")]
    [InlineData("COZMO_SESSION5_FACE_BUNDLE")]
    public async Task CapturedPixelsCompleteTeachRenameAndEraseThroughMcp(string capture)
    {
        bool musicWindow = capture == "COZMO_MAMBO_REPLAY_BUNDLE";
        bool sessionFive = capture == "COZMO_SESSION5_FACE_BUNDLE";
        string? sessionFiveBundle = sessionFive ? Environment.GetEnvironmentVariable(capture) : null;
        string? bundle = Environment.GetEnvironmentVariable(sessionFive ? "COZMO_FACE_REPLAY_BUNDLE" : capture);
        if (string.IsNullOrEmpty(bundle) || (sessionFive && string.IsNullOrEmpty(sessionFiveBundle))) return;
        var requests = sessionFiveBundle is null ? null : ReadSessionFiveFaceRequests(sessionFiveBundle);
        string originalName = requests?[0].GetProperty("arguments").GetProperty("name").GetString() ?? "Robin";
        string updatedName = requests?[1].GetProperty("arguments").GetProperty("newName").GetString() ?? "Riley";
        string? messagesSha256 = null;
        var pictures = new List<byte[]>();
        byte[]? calibration = null;
        var camera = new CozmoCamera();
        camera.FrameReceived += frame =>
        {
            // 0x46 confirms a tracked face throughout each selected camera interval.
            bool inView = musicWindow ? frame.Timestamp is >= 92825 and <= 94040
                : frame.Timestamp is >= 230005 and <= 231510;
            if (!inView) return;
            var cache = new ImageCache();
            if (frame.IsColor && frame.TryDecodeRgb(out var rgb, out _) && rgb is not null)
            {
                cache.Reset(rgb, frame.Width, frame.Height);
                pictures.Add(cache.GetGray().Pixels.ToArray());
            }
            else if (frame.TryDecodeGray(out var gray, out _) && gray is not null)
                pictures.Add(gray.Pixels.ToArray());
        };
        foreach (var message in ReadCameraInputs())
        {
            if (message is NVOpResult { Tag: CameraCalibration.NvEntryTag, Result: 0 } nv)
                calibration = nv.Data;
            if (message is ImageChunk chunk) camera.Handle(chunk);
        }

        IEnumerable<RobotMessage> ReadCameraInputs()
        {
            // F15 uses the raw capture from the same session as F18. Its
            // messages.jsonl export is empty, so read packets from frames.jsonl.
            if (musicWindow)
            {
                foreach (var row in RecordedRobotInput.ReadInbound(bundle,
                    at => at <= 30000 || at >= 93000, 95600, [0xCD, 0xF2]))
                    yield return row.Message;
                yield break;
            }
            string path = Path.Combine(bundle, "messages.jsonl");
            using (var capture = File.OpenRead(path))
                messagesSha256 = Convert.ToHexString(SHA256.HashData(capture));
            Assert.Equal("C67BA13DEC4EAE4538958E12CAD294883F7EB0C441A8A829E4DCD29BDB33AD63", messagesSha256);
            foreach (string line in File.ReadLines(path))
            {
                using var doc = JsonDocument.Parse(line);
                var row = doc.RootElement;
                if (row.GetProperty("outbound").GetBoolean()) continue;
                double at = row.GetProperty("atMs").GetDouble();
                string? tag = row.GetProperty("tag").GetString();
                if (tag == "0xCD" && at < 30000 || tag == "0xF2" && at is >= 247000 and <= 255800)
                    yield return RobotMessage.Parse(Convert.FromHexString(row.GetProperty("payloadHex").GetString()!));
            }
        }
        Assert.NotNull(calibration); Assert.NotEmpty(pictures);
        output.WriteLine("Captured pixels: " + string.Join(',', pictures.Select(p => Convert.ToHexString(SHA256.HashData(p)))));
        var options = new CozmoEngineOptions
        {
            ResourcesPath = PickupPyramidNativeExecutionTests.Resources(), BlockPoolPath = "",
            SpeechSoundBankDirectory = GraphRig.FindMeta(), LabLocaleLanguage = "en", LabLocaleCountry = "US",
        };
        using var sink = new SilentSink();
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true,
            sourceNvRaw: tag => tag == CameraCalibration.NvEntryTag ? calibration : null,
            engineOptions: options, engineCreated: e => e.RuntimeAudioSinkForTest = () => sink,
            keepNeedsFull: true);
        Assert.NotNull(f.Rig.Engine.RuntimeWwiseAudio);
        await f.Rig.Engine.RuntimeWwiseAudio!.Ready.WaitAsync(TimeSpan.FromSeconds(20));
        var vision = f.M.Vision;
        var detector = Assert.IsType<OkaoFaceDetector>(vision.FaceDetector);
        var inputPictures = new ConcurrentDictionary<uint, byte[]>();
        byte[]? stablePicture = null;
        detector.RecognitionFeatureConsumedForTest = (timestamp, track) =>
        {
            // Hold still on the first captured image that the actual recognizer
            // successfully registered. This observer supplies no recognition data.
            if (vision.Recognizer.EnrolledFaces.Any(face => face.CurrentTrack == track) &&
                inputPictures.TryGetValue(timestamp, out var pixels))
                Interlocked.CompareExchange(ref stablePicture, pixels, null);
        };
        Assert.Empty(vision.Recognizer.EnrolledFaces);
        f.Rig.Engine.ContextRandom.SetSeed(4069123563); // Recorded app.random_seed, controlled app input.
        f.Rig.Robot.SetEnableFreeplayActivity(false); f.Rig.Tick();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        var behavior = Assert.IsType<NativeEnrollFaceBehavior>(f.Runtime.Bound["EnrollFace"]);
        var renameBehavior = Assert.IsType<NativeRespondToRenameFace>(f.Runtime.Bound["RespondToRenameFace"]);
        bool renameResponseSeen = false; uint? renameActionTag = null;
        var actionCompletions = new List<RobotCompletedAction>();
        f.Robot.ActionList.RegisterActionEndedCallback(actionCompletions.Add);
        var reports = new ConcurrentQueue<FaceEnrollmentCompletedReport>();
        var seenFaces = new List<RobotObservedFaceReport>();
        f.Rig.Engine.RobotObservedFace += seenFaces.Add;
        var emissions = new List<Emission>();
        var faults = new ConcurrentQueue<Exception>();
        var missing = new ConcurrentQueue<string>();
        f.Rig.Engine.FaceEnrollmentCompleted += reports.Enqueue;
        f.Rig.Robot.HandlerFaulted += faults.Enqueue;
        f.Rig.Engine.StackLogLine += s => { if (s.Contains("MISSING", StringComparison.Ordinal)) missing.Enqueue(s); };
        f.Rig.Engine.EngineToGameMessage += (tag, body) =>
        {
            if (tag is 0x46 or 0x47 or 0x56 or 0x57 or 0x1D or 0x8B)
                emissions.Add(new(tag, Convert.ToHexString(body.Span), f.Rig.Engine.Timer.TimeStampMs, f.Rig.Engine.NvStorage!.IsIdle));
        };
        int sent = f.Rig.Mark(); uint imageId = 60000; int imageIndex = 0;
        byte? animationTag = null;
        var diagnostics = new List<string>(); double nextDiagnostic = 0;
        var phases = new HashSet<int>(); var clips = new HashSet<string>();
        var nvCommands = new List<object>(); int audioChunks = 0;
        f.Rig.DefaultTickMs = 0;
        long start = f.Rig.NowNs;
        var wall = Stopwatch.StartNew(); double nextPicture = 0;
        async Task PumpUntil(Func<bool> done, double seconds, bool sendPictures = true)
        {
            double until = wall.Elapsed.TotalSeconds + seconds;
            while (!done() && wall.Elapsed.TotalSeconds < until)
            {
                f.Rig.NowNs = start + wall.Elapsed.Ticks * 100;
                var stream = f.Rig.Robot.Animations.Scheduler.Stream;
                f.Rig.Data(new AnimationState { NumAudioFramesPlayed = stream.FramesStreamed, NumAnimBytesPlayed = stream.BytesStreamed });
                f.Motors(RobotStatusFlag.IsBodyAccMode);
                if (sendPictures && wall.Elapsed.TotalMilliseconds >= nextPicture)
                {
                    nextPicture = wall.Elapsed.TotalMilliseconds + 65;
                    byte[] pixels = Volatile.Read(ref stablePicture) ?? pictures[imageIndex % pictures.Count];
                    imageIndex++;
                    inputPictures[f.Robot.StoredState!.Timestamp] = pixels;
                    f.NativeCameraFrame(imageId++, pixels);
                    f.Rig.Tick();
                }
                phases.Add(behavior.Phase);
                if (renameBehavior.EngineRunning)
                {
                    renameResponseSeen = true;
                    if (renameBehavior.OwnedRunner is { } runner) renameActionTag = runner.Tag;
                }
                if (wall.Elapsed.TotalMilliseconds >= nextDiagnostic)
                {
                    nextDiagnostic = wall.Elapsed.TotalMilliseconds + 2000;
                    string Tree(IActionRunner runner) => runner.GetType().Name + ":" + runner.State.ToString("X8") +
                        (runner is NativeLastFaceRunner face ? $" faceState={face.FaceState} verified={face.VerifiedId}" : "") +
                        (runner is CompoundAction compound ? "[" + string.Join(',', compound.Children.Select(Tree)) + "]" : "");
                    string audioState = "";
                    if (renameBehavior.OwnedRunner is SayTextAction speech)
                    {
                        var lease = (CannedSpeechLease?)typeof(SayTextAction).GetField("_cannedSession", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(speech);
                        var path = lease?.Path;
                        audioState = $" renameText={speech.Text} renameChild={speech.ChildForTest?.GetAnimationCompletionName()} renameTag={speech.ChildForTest?.AnimationTagForTest} path={path?.State} callbacks={path?.CallbacksReceived}/{path?.PostedCount} cursor={path?.EventCursor} events={string.Join(',', path?.Events.Select(e => $"{e.EventId:X8}:{e.State}@{e.TriggerTimeMs}") ?? [])}";
                    }
                    diagnostics.Add($"t={wall.Elapsed.TotalSeconds:F2} phase={behavior.Phase} selected={behavior.Selected} remaining={vision.Recognizer.EnrollmentCount} last={string.Join(',', vision.LastFaces.Select(x => x.Id))} tree={(behavior.OwnedRunner is { } r ? Tree(r) : "none")} currentBehavior={f.Robot.BehaviorManagerOwner!.Current?.Id} renameRunning={renameBehavior.EngineRunning} renameTree={(renameBehavior.OwnedRunner is { } rr ? Tree(rr) : "none")}{audioState}");
                }
                var messages = f.Rig.SentSince(sent); sent += messages.Count;
                foreach (var message in messages)
                {
                    switch (message)
                    {
                        case StartOfAnimation animation:
                            animationTag = animation.AnimId;
                            if (f.Rig.Robot.Animations.Scheduler.Playing is { } clip) clips.Add(clip);
                            f.Rig.Data(new AnimationStarted { AnimId = animation.AnimId }); break;
                        case EndOfAnimation when animationTag is { } tag:
                            f.Rig.Data(new AnimationEnded { AnimId = tag }); animationTag = null; break;
                        case ExecutePath path:
                            f.Rig.Data(new PathFollowingEvent { EventId = path.EventId, EventType = 0 });
                            f.Rig.Tick();
                            f.Rig.Data(new PathFollowingEvent { EventId = path.EventId, EventType = 2 }); break;
                        case NVCommand nv:
                            nvCommands.Add(new { nv.Tag, nv.Op, nv.Length });
                            f.Rig.Data(new NVOpResult { Tag = nv.Tag, Op = nv.Op, Result = 0 }); break;
                        case AudioSample: audioChunks++; break;
                    }
                }
                f.Rig.Tick(); f.Rig.Engine.DrainAppDelivery();
                await Task.Delay(10);
            }
        }
        int teach = requests is null
            ? await client.Send("tools/call", new { name = "enroll-face", arguments = new { name = originalName } })
            : await client.Send("tools/call", requests[0]);
        await PumpUntil(() => !reports.IsEmpty, 100);
        output.WriteLine($"phase={behavior.Phase}; phases={string.Join(',', phases)}; faces={string.Join(',', vision.Recognizer.EnrolledFaces.Select(x => x.FaceID + ":" + x.Name))}; counter={vision.Recognizer.EnrollmentCount}; pictures={imageIndex}; audio={audioChunks}");
        foreach (string diagnostic in diagnostics) output.WriteLine(diagnostic);
        output.WriteLine("seen IDs=" + string.Join(',', seenFaces.Select(x => x.FaceId).Distinct()));
        output.WriteLine($"faceReports={seenFaces.Count}; lastFaces={string.Join(',', vision.LastFaces.Select(x => x.Id))}");
        foreach (string line in missing.Distinct()) output.WriteLine(line);
        foreach (string entry in f.Rig.Log.Where(s => s.Contains("Face", StringComparison.Ordinal) || s.Contains("Enroll", StringComparison.Ordinal)).TakeLast(60)) output.WriteLine(entry);
        Assert.NotEmpty(reports);
        var completion = Assert.Single(reports);
        Assert.Equal(0, completion.Result); Assert.True(completion.Id > 0); Assert.Equal(originalName, completion.Name);
        string ExpectedBody(string name, byte? result = null)
        {
            using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes);
            if (result is { } code) writer.Write(code);
            writer.Write(completion.Id);
            byte[] text = System.Text.Encoding.UTF8.GetBytes(name);
            writer.Write(checked((byte)text.Length)); writer.Write(text);
            return Convert.ToHexString(bytes.ToArray());
        }
        Assert.Equal(ExpectedBody(originalName, 0), Assert.Single(emissions, e => e.Tag == 0x56).Body);
        // 5FE462..468: objective EnrolledFaceWithName=5, broadcast=true,
        // before 56. 7285FE -> tag8B; 71BEAE..B6 packs the four-byte enum.
        Assert.Single(emissions, e => e.Tag == 0x8B && e.Body == "05000000");
        Assert.True(emissions.FindIndex(e => e.Tag == 0x8B && e.Body == "05000000") <
            emissions.FindIndex(e => e.Tag == 0x56));
        var taught = await client.Receive(teach);
        var taughtData = taught.GetProperty("result").GetProperty("structuredContent");
        output.WriteLine("teach=" + taughtData.GetProperty("report").GetRawText());
        Assert.False(taught.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("enrolled", taughtData.GetProperty("faceOperation").GetProperty("outcome").GetString());
        Assert.Equal(completion.Id, taughtData.GetProperty("faceOperation").GetProperty("faceId").GetInt32());
        Assert.Equal(originalName, taughtData.GetProperty("faceOperation").GetProperty("name").GetString());
        Assert.Null(f.Runtime.MeetCozmoAdmission);
        var afterTeach = (await client.Tool("get_state", new { })).GetProperty("result").GetProperty("structuredContent");
        Assert.True(afterTeach.GetProperty("freeplay").GetProperty("active").GetBoolean()); // Hub G2E 0x92.
        Assert.Contains(3, phases); Assert.Contains(4, phases); Assert.True(audioChunks > 0);
        // The native say-name music reached the platform. The MCP command's
        // app exit then supplies the bank's Stop event with its 500 ms fade.
        Assert.True(sink.LastAudibleFrame > 0);
        long framesAtExit = sink.SubmittedFrames;
        await PumpUntil(() => sink.SubmittedFrames >= framesAtExit + 48000, 4);
        Assert.True(sink.SubmittedFrames >= framesAtExit + 48000);
        Assert.True(sink.SubmittedFrames - sink.LastAudibleFrame >= 10240,
            "The platform music should fall silent after the native fade and queued output.");
        // 0x005FE314 resets enrollment before the separate 0x56 publisher.
        // Named 0x46 comes from a later processed frame (0x00655274 ->
        // 0x004F4B7A/82), not from the save/completion callback. Keep feeding
        // actual pixels until that observation arrives. This 30-second guard
        // is harness liveness, not a claimed engine report-order deadline.
        int beforePostEnrollmentObservation = seenFaces.Count;
        await PumpUntil(() => seenFaces.Skip(beforePostEnrollmentObservation).Any(face => face.FaceId == completion.Id && face.Name == originalName), 30);
        output.WriteLine("post-enroll seen names=" + string.Join(',', seenFaces.Select(face => face.Name).Distinct()));
        Assert.Contains(seenFaces.Skip(beforePostEnrollmentObservation), face => face.FaceId == completion.Id && face.Name == originalName);
        Assert.True(Assert.Single(emissions, e => e.Tag == 0x56).NvIdle);
        Assert.Contains(emissions, e => e.Tag == 0x47);
        Assert.DoesNotContain(missing, line => line.Contains("M14-006", StringComparison.Ordinal));
        Assert.Equal(originalName, Assert.Single(vision.Recognizer.EnrolledFaces).Name);
        if (musicWindow)
        {
            Assert.Empty(faults);
            Assert.DoesNotContain(missing, line => line.Contains("M1-064", StringComparison.Ordinal));
            return;
        }
        async Task<JsonElement> KnownFaces() => (await client.Tool("get_state", new { }))
            .GetProperty("result").GetProperty("structuredContent").GetProperty("knownFaces");
        var renamed = new ConcurrentQueue<FaceRenamedReport>();
        f.Rig.Engine.FaceRenamed += renamed.Enqueue;
        // The recorded album used ID 4. Replay against this fixture's newly allocated ID.
        int rename = await client.Send("tools/call", new { name = "rename-face",
            arguments = new { faceId = completion.Id, oldName = originalName, newName = updatedName } });
        await PumpUntil(() => !renamed.IsEmpty, 10);
        Assert.Equal(new FaceRenamedReport(completion.Id, updatedName), Assert.Single(renamed));
        var renamedReply = await client.Receive(rename);
        output.WriteLine("rename=" + renamedReply.GetProperty("result").GetProperty("structuredContent").GetProperty("report").GetRawText());
        var renameData = renamedReply.GetProperty("result").GetProperty("structuredContent");
        Assert.Equal("renamed", renameData.GetProperty("faceOperation").GetProperty("outcome").GetString());
        Assert.Equal(updatedName, renameData.GetProperty("faceOperation").GetProperty("name").GetString());
        Assert.False(Assert.Single(emissions, e => e.Tag == 0x1D).NvIdle);
        Assert.Equal(ExpectedBody(updatedName), Assert.Single(emissions, e => e.Tag == 0x1D).Body);
        var renamedState = await KnownFaces();
        var renamedFace = Assert.Single(renamedState.EnumerateArray(), f => f.GetProperty("faceId").GetInt32() == completion.Id);
        Assert.Equal(updatedName, renamedFace.GetProperty("name").GetString());
        Assert.True(renamedFace.GetProperty("enrolled").GetBoolean());
        await PumpUntil(() => seenFaces.Any(face => face.FaceId == completion.Id && face.Name == updatedName), 30);
        Assert.Contains(seenFaces, face => face.FaceId == completion.Id && face.Name == updatedName);
        // E2G 0x1D acknowledges the mutation, independently of Meet's
        // response-speech admission. An existing reaction can retain the turn
        // (MeetCozmoAdmissionTests); do not invent a 30-second speech deadline.
        var erased = new ConcurrentQueue<FaceErasedReport>();
        f.Rig.Engine.FaceErased += erased.Enqueue;
        int erase = await client.Send("tools/call", new { name = "erase-face", arguments = new { faceId = completion.Id } });
        // Session 5 F4: read state after the erase report before a fresh observation can replace the cached name.
        await PumpUntil(() => !erased.IsEmpty, 10, sendPictures: false);
        Assert.Equal(completion.Id, Assert.Single(erased).Id);
        var erasedReply = await client.Receive(erase);
        output.WriteLine("erase=" + erasedReply.GetProperty("result").GetProperty("structuredContent").GetProperty("report").GetRawText());
        var eraseData = erasedReply.GetProperty("result").GetProperty("structuredContent");
        Assert.Equal("erased", eraseData.GetProperty("faceOperation").GetProperty("outcome").GetString());
        Assert.Equal(updatedName, eraseData.GetProperty("faceOperation").GetProperty("lastKnownName").GetString());
        Assert.Equal("", eraseData.GetProperty("report").GetProperty("name").GetString());
        Assert.False(Assert.Single(emissions, e => e.Tag == 0x57).NvIdle);
        Assert.Equal(ExpectedBody(""), Assert.Single(emissions, e => e.Tag == 0x57).Body);
        Assert.Contains(f.M.Vision.Faces.Faces, face => face.Id == completion.Id && face.Name == updatedName);
        var erasedState = await KnownFaces();
        Assert.DoesNotContain(erasedState.EnumerateArray(), face =>
            face.GetProperty("faceId").GetInt32() == completion.Id &&
            (face.GetProperty("enrolled").GetBoolean() || !string.IsNullOrEmpty(face.GetProperty("name").GetString())));
        Assert.True(emissions.FindIndex(e => e.Tag == 0x56) < emissions.FindIndex(e => e.Tag == 0x1D));
        Assert.True(emissions.FindIndex(e => e.Tag == 0x1D) < emissions.FindIndex(e => e.Tag == 0x57));
        await PumpUntil(() => f.Rig.Engine.NvStorage!.IsIdle, 10);
        // Native EraseFace removes the named entry, not the visible person.
        // Continuing images may auto-register a new unnamed guest (0x00865658).
        output.WriteLine("afterErase=" + string.Join(',', vision.Recognizer.EnrolledFaces.Select(face => face.FaceID + ":" + face.Name)));
        // GetNextFaceID 0x0086561C returns a free current ID without advancing;
        // a newly registered unnamed guest may therefore even reuse this ID.
        Assert.DoesNotContain(vision.Recognizer.EnrolledFaces, face => face.Name.Length != 0);
        Assert.DoesNotContain(missing, line => line.Contains("M14-006", StringComparison.Ordinal));
        Assert.Empty(faults);
        var summary = new { input = new { messagesSha256,
                frameTimestampStart = 230005u, frameTimestampEnd = 231510u,
                frozenImageSha256 = stablePicture is null ? null : Convert.ToHexString(SHA256.HashData(stablePicture)),
                seed = 4069123563u, freshAlbum = true }, phases, clips, nvCommands, audioChunks, emissions,
            renameSpeech = new { admitted = renameResponseSeen, tag = renameActionTag,
                completed = renameResponseSeen && !renameBehavior.EngineRunning,
                actionResults = actionCompletions.Where(c => c.Tag == renameActionTag).Select(c => c.Result).ToArray() } };
        string? reportPath = Environment.GetEnvironmentVariable("COZMO_TEACH_FACE_REPORT");
        if (!string.IsNullOrEmpty(reportPath)) File.WriteAllText(reportPath, JsonSerializer.Serialize(summary));
        output.WriteLine(JsonSerializer.Serialize(new { phases, clips, nvCommands, audioChunks, emissions = emissions.Count }));
    }

    // PCM device boundary only; native audio initialization/rendering remains active.
    private sealed class SilentSink : IPlatformPcmSink
    {
        private readonly object gate = new();
        private readonly AutoResetEvent ready = new(false);
        private readonly Queue<(ulong Token, double Due)> pending = new();
        private readonly Queue<ulong> done = new();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Timer timer;
        private double tail;
        private long submittedFrames, lastAudibleFrame;
        internal long SubmittedFrames { get { lock (gate) return submittedFrames; } }
        internal long LastAudibleFrame { get { lock (gate) return lastAudibleFrame; } }
        private bool disposed;
        internal SilentSink() => timer = new Timer(_ => Complete(), null, 1, 1);
        public WaitHandle CompletionReady => ready;
        public void Submit(ulong token, byte[] pcm)
        {
            lock (gate)
            {
                if (disposed) return;
                submittedFrames += pcm.Length / 4; // 48 kHz stereo, signed 16-bit interleaved
                if (pcm.Any(sample => sample != 0)) lastAudibleFrame = submittedFrames;
                tail = Math.Max(tail, clock.Elapsed.TotalMilliseconds) + pcm.Length / 192.0;
                pending.Enqueue((token, tail));
            }
        }
        private void Complete()
        {
            lock (gate)
            {
                if (disposed) return;
                bool completed = false;
                while (pending.TryPeek(out var item) && item.Due <= clock.Elapsed.TotalMilliseconds)
                { pending.Dequeue(); done.Enqueue(item.Token); completed = true; }
                if (completed) ready.Set();
            }
        }
        public IReadOnlyList<ulong> DrainCompleted()
        { lock (gate) { var result = done.ToArray(); done.Clear(); return result; } }
        public void Dispose()
        { lock (gate) { if (disposed) return; disposed = true; timer.Dispose(); ready.Dispose(); } }
    }

    private static JsonElement[] ReadSessionFiveFaceRequests(string bundle)
    {
        var requests = new List<JsonElement>();
        // Replay the captured MCP requests with the established Nov 17 camera fixture.
        // The Session 5 raw camera slices did not reacquire a face in this isolated rig.
        foreach (string line in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
        {
            using var json = JsonDocument.Parse(line);
            var row = json.RootElement;
            double at = row.GetProperty("atMs").GetDouble();
            if (at > 472000) break;
            if (at < 431000 || row.GetProperty("kind").GetString() != "mcp-input" ||
                row.GetProperty("method").GetString() != "tools/call") continue;
            var call = row.GetProperty("params");
            if (call.GetProperty("name").GetString() is "teach-face" or "rename-face" or "erase-face")
                requests.Add(call.Clone());
        }

        Assert.Equal(new[] { "teach-face", "rename-face", "erase-face" },
            requests.Select(call => call.GetProperty("name").GetString()));
        Assert.Equal(requests[0].GetProperty("arguments").GetProperty("name").GetString(),
            requests[1].GetProperty("arguments").GetProperty("oldName").GetString());
        Assert.Equal(requests[1].GetProperty("arguments").GetProperty("faceId").GetInt32(),
            requests[2].GetProperty("arguments").GetProperty("faceId").GetInt32());
        return requests.ToArray();
    }
}
