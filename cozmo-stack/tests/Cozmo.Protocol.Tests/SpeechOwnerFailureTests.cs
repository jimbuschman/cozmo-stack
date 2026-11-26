using System.IO.Compression;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Emulation;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
using static Cozmo.Protocol.Tests.BridgeEventPushTests;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class SpeechOwnerFailureTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ReadyRobotSpeechReportsNativeAbortForActualHostAudioOrProviderFailure(bool emptyArchive, bool synthesisFails)
    {
        if (!OperatingSystem.IsWindows()) return;
        string archive = Path.Combine(Path.GetTempPath(), $"cozmo-missing-init-scene-{Guid.NewGuid():N}.zip");
        bool previousError = Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
            if (emptyArchive) using (ZipFile.Open(archive, ZipArchiveMode.Create)) { }
            var reports = new System.Collections.Concurrent.ConcurrentQueue<EngineLogEntry>();
            var startupLog = new System.Collections.Concurrent.ConcurrentQueue<string>();
            var options = new CozmoEngineOptions
            {
                ResourcesPath = PickupPyramidNativeExecutionTests.Resources(), BlockPoolPath = "", SpeechAudioArchivePath = archive,
                LabLocaleLanguage = "en", LabLocaleCountry = "US",
            };
            using var f = new Fixture(sourceNvEmpty: true, keepNeedsFull: true, engineOptions: options,
                engineCreated: e =>
                {
                    e.LogEntry += reports.Enqueue;
                    e.StackLogLine += startupLog.Enqueue;
                    e.RuntimeAudioSinkForTest = () => throw new InvalidOperationException("No audio should open for absent banks");
                });
            Assert.True(f.Rig.Engine.RuntimeAudioStarted);
            string expected = Assert.IsType<string>(f.Rig.Engine.RuntimeAudioStartupFailure);
            Assert.Contains(emptyArchive ? "Init.bnk" : "archive", expected, StringComparison.OrdinalIgnoreCase);
            if (emptyArchive)
                Assert.Contains(reports, r => r.Name == "AudioEngineController.LoadSoundbank" && r.Text == "Failed to load soundbank 'Init.bnk'");
            f.Rig.Robot.SetEnableFreeplayActivity(false); f.Rig.Tick();
            Assert.Contains(startupLog, l => l.Contains("M6-024 InitScene failed:"));
            Assert.DoesNotContain(startupLog, l => l.Contains("MISSING M6-024 InitScene"));
            f.Rig.Robot.DirectSpeechComponentForTest = new TextToSpeechComponent((_, _, _) => synthesisFails
                ? throw new AcapelaTtsException("nLoadVoice", -2) : new[] { .25f, -.25f });
            using var bridge = new CozmoBridge(f.Rig.Robot);
            await using var client = new McpServerTests.Client(bridge);
            await client.Initialize();
            Assert.True(Data(await client.Tool("get_state", new { })).GetProperty("connection").GetProperty("ready").GetBoolean());
            Cozmo.Transport.EngineErrorState.ErrorFlagSet = false;
            int mark = f.Rig.Mark();
            int request = await client.Send("tools/call", new { name = "say-text", arguments = new { text = "hello" } });
            var runner = Assert.IsType<SayTextAction>(f.Robot.ActionList.GetCurrentAction());
            uint tag = runner.Tag;
            // The worker may complete asynchronously. Tick the real ActionList,
            // which reads the byte operation state and emits E2G 0x5A on abort.
            for (int i = 0; i < 100 && f.Robot.ActionList.ContainsRunner(runner); i++)
            { await Task.Yield(); f.Rig.Tick(); }
            var result = Data(await client.Receive(request));
            Assert.True(result.GetProperty("admitted").GetBoolean());
            Assert.Equal(0x03000000u, result.GetProperty("engineResult").GetUInt32()); // 0x00561630
            Assert.DoesNotContain(f.Rig.SentSince(mark), m => m is StartOfAnimation or AudioSample);
            var completion = Assert.Single(bridge.ReadEvents().GetProperty("events").EnumerateArray(), e => Type(e) == "action-completed" &&
                e.GetProperty("payload").GetProperty("completion").GetProperty("tag").GetUInt32() == tag);
            Assert.Equal(0x03000000u, completion.GetProperty("payload").GetProperty("completion").GetProperty("result").GetUInt32());
            if (synthesisFails) Assert.Contains(f.Rig.Log, line => line.Contains("speech synthesis failed: Acapela nLoadVoice failed: -2"));
            else
            {
                Assert.Contains(reports, r => r.Name == "SayTextAction.Init.PrepareAudioEngine.Failed");
                Assert.True(Cozmo.Transport.EngineErrorState.ErrorFlagSet);
                Assert.Contains(expected, result.GetProperty("detail").GetString());
            }
        }
        finally
        {
            Cozmo.Transport.EngineErrorState.ErrorFlagSet = previousError;
            if (File.Exists(archive)) File.Delete(archive);
        }
    }
}
