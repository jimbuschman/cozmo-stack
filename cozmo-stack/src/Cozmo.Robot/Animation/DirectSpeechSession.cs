using Cozmo.Robot.Animation.Wwise;
using Cozmo.Robot.Emulation;

namespace Cozmo.Robot.Animation;

// Session-owned adapters. Native PostEvent runs on the independent dispatch thread, not the streamer lock.
internal interface IDirectSpeechSession
{
    WwiseRobotAudioPath Path { get; }
    Exception? Failure { get; }
    void Retire();
}

internal sealed class DirectSpeechSession : IWwiseRobotAudioHost, IDirectSpeechSession
{
    private readonly SpeechRobotAudioBuffer _buffer;
    private readonly Action<string> _report;
    internal NativeWwiseVoiceSession Native { get; }
    internal WwiseRobotAudioPath Path { get; }
    WwiseRobotAudioPath IDirectSpeechSession.Path => Path;
    Exception? IDirectSpeechSession.Failure => Native.Failure;
    private DirectSpeechSession(SpeechRobotAudioBuffer buffer, NativeWwiseVoiceSession native, Action<string> report)
    {
        _buffer = buffer;
        Native = native;
        _report = report;
        Path = new WwiseRobotAudioPath(this, native, new DispatchFactory(), (_, _) => 0);
    }
    internal static async Task<DirectSpeechSession> Open(string library, string banks, float[] samples, Action<string> report)
    {
        var init = await File.ReadAllBytesAsync(PathCombine(banks, "Init.bnk")).ConfigureAwait(false);
        var english = await File.ReadAllBytesAsync(PathCombine(banks, "English(US)", "Cozmo.bnk")).ConfigureAwait(false);
        var buffer = new SpeechRobotAudioBuffer();
        var native = await NativeWwiseVoiceSession.OpenAsync(library, init, english, buffer, () => Environment.TickCount64).ConfigureAwait(false);
        try { await native.OfferAsync(samples, 22050).ConfigureAwait(false); return new(buffer, native, report); }
        catch { ObserveClose(native.CloseAsync(), report); throw; }
    }
    private static string PathCombine(params string[] parts) => System.IO.Path.Combine(parts);
    private static void ObserveClose(Task close, Action<string> report) => _ = close.ContinueWith(t =>
    {
        if (t.Exception is { } error) report($"M14-006 speech owner shutdown failed: {error.GetBaseException().Message}");
    }, TaskScheduler.Default);
    internal void Retire() { _buffer.Retire(); ObserveClose(Native.CloseAsync(), _report); }
    void IDirectSpeechSession.Retire() => Retire();
    public void SetGameObjectAuxSendValues(uint obj, uint bus, float gain) { }
    public void SetGameObjectOutputBusVolume(uint obj, float volume) { }
    public void RegisterRobotAudioBuffer(uint obj, int plugin) { }
    public IWwiseRobotAudioBuffer? GetRobotAudioBuffer(uint obj) => obj == 7 ? _buffer : null;

    private sealed class DispatchFactory : IWwiseDispatchFactory
    {
        public IWwiseDispatchQueue Create(int priority) => new Dispatch();
    }
    private sealed class Dispatch : IWwiseDispatchQueue
    {
        private readonly CancellationTokenSource _stop = new();
        public void After(double delayMs, Action action)
        {
            _ = Task.Run(async () =>
            {
                try { if (delayMs > 0) await Task.Delay(TimeSpan.FromMilliseconds(delayMs), _stop.Token).ConfigureAwait(false); if (!_stop.IsCancellationRequested) action(); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { System.Diagnostics.Trace.TraceError("M14-006 speech dispatch failed: " + ex.Message); }
            });
        }
        public void Stop() => _stop.Cancel();
    }
}
