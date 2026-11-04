namespace Cozmo.Robot.Emulation;

/// <summary>A failed Acapela step: which native, and the code it returned.</summary>
public sealed class AcapelaTtsException : Exception
{
    public AcapelaTtsException(string step, int code) : base($"Acapela {step} failed: {code}") { Step = step; Code = code; }
    public string Step { get; }
    public int Code { get; }
}

/// <summary>
/// Acapela speech from the shipped libacattsandroid.so under the emulator (<see cref="AcapelaNative"/>), driven in the order
/// the app's Java glue drives it:
/// <list type="bullet">
/// <item>construction: <c>new acattsandroid(...)</c> calls nInitCallbacks, then CozmoTextToSpeech's ctor calls getVersion
/// (nGetVersion) (CozmoTextToSpeech.java ctor; acattsandroid.java ctor);</item>
/// <item><see cref="LoadVoice"/>: doLoadVoice: getVoicesList (nGetVoiceList), setLicense (nSetLicense), load(voice, "")
/// (nLoadVoice, accepted when it returns 8000/11025/16000/22050), setTTSSettings LEADINGSILENCE 50 and TRAILINGSILENCE 50
/// (CozmoTextToSpeech.java:89-119);</item>
/// <item><see cref="Synthesize"/>: doCreateAudioData: setSpeechRate (nSetSpeechRate), setTTSSettings VOICESHAPE
/// (nSetTTSSetting), setPitch (nSetShape), speak (nSpeak) (CozmoTextToSpeech.java:121-156).</item>
/// </list>
/// Not called: the Java searchVoices' nGetVoiceInfo(ini, "speaker") for each .ini found (File.list order), and speak()'s
/// stop() (nStop) before and after nSpeak; the output was checked to be the same with and without the probe's single
/// nGetVoiceInfo call. The licence is the engine's own,
/// decoded by <see cref="AcapelaCredentials.FromEngine"/>. Output: 22050 Hz mono s16, the samples the library hands to the
/// Java samplesCallback.
/// </summary>
public sealed class AcapelaTts : IDisposable
{
    public const int SampleRate = 22050;
    private readonly AcapelaNative _n;
    private readonly string _libDir;

    public AcapelaTts(string libDir)
    {
        _libDir = libDir;
        _n = new AcapelaNative(libDir);
        try
        {
            _n.InitCallbacks();
            Version = _n.GetVersion();
        }
        catch
        {
            _n.Dispose();
            throw;
        }
    }

    public string? Version { get; }
    public AcapelaNative Native => _n;
    /// <summary>nLoadVoice's result: the voice's sample rate once loaded.</summary>
    public int VoiceFrequency { get; private set; }
    /// <summary>nSpeak's result for the last <see cref="Synthesize"/> (the Java glue does not check it).</summary>
    public int LastSpeakResult { get; private set; }

    /// <summary>
    /// Loads a voice from <paramref name="voiceDir"/> (the shipped <c>cozmo_resources/tts</c>, holding <c>Voices/</c>), seen by
    /// the library as <see cref="AcapelaNative.VirtualRoot"/>. <paramref name="credentials"/> defaults to the engine's.
    /// Throws <see cref="AcapelaTtsException"/> naming the failing native.
    /// </summary>
    public void LoadVoice(string voiceDir, string voice, AcapelaCredentials? credentials = null)
    {
        credentials ??= AcapelaCredentials.FromEngine(_libDir);
        _n.HostRoot = voiceDir;
        _n.GetVoiceList(AcapelaNative.VirtualRoot);
        int err = _n.SetLicense(credentials.UserId, credentials.Password, credentials.License);
        if (err != 0) throw new AcapelaTtsException("nSetLicense", err);
        int freq = _n.LoadVoice(voice, "");
        if (freq is not (8000 or 11025 or 16000 or 22050)) throw new AcapelaTtsException("nLoadVoice", freq);
        VoiceFrequency = freq;
        int e3 = _n.SetTtsSetting("LEADINGSILENCE", 50);
        if (e3 != 0) _n.Machine.Log.Add($"TextToSpeech.CozmoTextToSpeech.doLoadVoice Unable to set leading silence, err={e3}");
        int e4 = _n.SetTtsSetting("TRAILINGSILENCE", 50);
        if (e4 != 0) _n.Machine.Log.Add($"TextToSpeech.CozmoTextToSpeech.doLoadVoice Unable to set trailing silence, err={e4}");
    }

    /// <summary>Synthesises <paramref name="text"/> as given (no name substitution here) and returns the samples.</summary>
    public short[] Synthesize(string text, int rate, int shaping, int pitch)
    {
        if (VoiceFrequency == 0) throw new InvalidOperationException("no voice loaded");
        int e1 = _n.SetSpeechRate(rate);
        if (e1 != 0) throw new AcapelaTtsException("nSetSpeechRate", e1);
        int e2 = _n.SetTtsSetting("VOICESHAPE", shaping);
        if (e2 != 0) throw new AcapelaTtsException("nSetTTSSetting VOICESHAPE", e2);
        int e3 = _n.SetShape(pitch);
        if (e3 != 0) throw new AcapelaTtsException("nSetShape", e3);
        _n.Samples.Clear();
        LastSpeakResult = _n.Speak(text);
        return _n.Samples.ToArray();
    }

    public void Dispose() => _n.Dispose();
}
