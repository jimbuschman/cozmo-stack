using Cozmo.Protocol;

namespace Cozmo.Robot;

// fidelity: M3-041
/// <summary>
/// The IMU diagnostic file logger of the engine's Messaging (RobotToEngineImplMessaging): HandleImuData (tag 0xBF,
/// 0x00535C5E..0x00536060) and HandleImuRawData (tag 0xC7, 0x00536122..0x005365B2), the shared 32-bit counter at
/// Messaging+0x3C, the one ofstream at +0x40 / filebuf at +0x44, and the filebuf destructor's close on robot removal
/// (~Messaging 0x00532A48 then 0x00532A64 -> 0x005010B4 -> close 0x0050111C). Rows L1..L36 of
/// re-analysis/research/20261009-M3-041-build-rows.md, I1..I11 of 20261009-M1-046-imu-reachability.md.
/// Entry on the live path: <see cref="EngineRobot.InitSubscriptions"/> subscribes both tags with the Messaging handlers
/// (I2) and <see cref="CozmoEngine.Broadcast"/> delivers every received RobotToEngine message to them. Nothing gates
/// them: no debug flag, no SDK mode, no time-sync or robot-state test (L4, L16).
///
/// Host policy (the DataPlatform base directory and the phone's stdio are external, rows L5/L6/L23): the directory
/// the engine resolves as DataPlatform's string at +0xC is <see cref="CozmoEngineOptions.DataPlatformPersistentPath"/>
/// (default: the user's local application data folder, under "cozmo-stack"); stat/mkdir/fopen/fwrite/fflush/fclose are
/// .NET file primitives behind the FILE* (the phone's stdio); the filebuf put area itself is the explicit port
/// <see cref="EngineFilebuf"/> (setbuf(null, 4096), F1.3/F1.4), and a short write is the engine's failed output (L31/L32, F2.4).
/// </summary>
internal sealed class ImuDiagnosticLog
{
    private const int GoodBit = 0, BadBit = 1, FailBit = 4;      // ios_base::iostate

    private readonly EngineRobot _robot;
    private uint _counter;                                         // Messaging+0x3C (L1, I3): one shared word, initially 0
    private int _state = GoodBit;                                  // ofstream ios state; exception mask 0 (L2)
    private readonly EngineFilebuf _buf = new();                         // the filebuf at Messaging+0x44 (F1.1..F1.5)

    internal ImuDiagnosticLog(EngineRobot robot) => _robot = robot;

    internal uint Counter => _counter;
    internal bool IsOpen => _buf.IsOpen;
    internal int StreamState => _state;

    // fidelity: M3-042
    /// <summary>The DataPlatform persistent directory when the host names none.</summary>
    internal static string DefaultPersistentPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cozmo-stack");

    private void Log(string line) => _robot.Engine.Log(line);

    // G1: sChanneledInfoF (PLT 0x004A505C) with channel "Unnamed" (0x00BE3FEC), the event name and the format's text, in the
    // stack's "info: [channel] event: text" convention. OpeningLogFile's format is "%s" with the file name; ClosingLogFile's
    // format is empty (0x00BE3F00). The composition of the final line inside sChanneledInfoF is outside the inventory.
    private void LogInfo(string eventName, string text) => Log($"info: [Unnamed] {eventName}: {text}");

    /// <summary>
    /// L5/L6: kP_IMU_LOGS_DIR is "imu_logs" in scope 2; the path is the DataPlatform string at +0xC, a '/' because the
    /// resource does not begin with one, then the resource.
    /// </summary>
    private string LogsDirectory()
        => (_robot.Engine.Options.DataPlatformPersistentPath ?? DefaultPersistentPath) + "/imu_logs";

    // L23/L24, F6.1/F7.1: ofstream.open(name, out). filebuf.open fails, without touching the FILE, cm or the put area, when
    // a FILE is already open; otherwise fopen "w"; the stream state is then cleared to 0, and a failure ORs failbit into the
    // old state.
    private void Open(string path)
    {
        if (_buf.Open(path)) _state = GoodBit;
        else _state |= FailBit;
    }

    // L31/L32, F2.4..F2.7: one insertion = one __put_character_sequence / num_put call. The sentry skips the whole
    // insertion when rdstate != 0 (no output, no state change); otherwise sputn(n) is called and a short count makes
    // clear(rdstate | badbit | failbit) = |5. There is no flush per insertion (flags 0x1002 carry no unitbuf).
    // Boundary (F2.5, open question 3, MISSING in the inventory): how many sputn calls num_put::do_put makes for one
    // float or int is libc++_shared code not read; this port makes ONE sputn call per formatted number.
    private void Insert(string text)
    {
        if (_state != GoodBit) return;
        var bytes = new byte[text.Length];
        for (int i = 0; i < text.Length; ++i) bytes[i] = (byte)text[i];
        if (_buf.Xsputn(bytes) != bytes.Length) _state |= BadBit | FailBit;
    }

    // L15/L22/L25, F5.4: after the diagnostic, filebuf.close directly; a null result is clear(old state | failbit).
    private void CloseFromHandler()
    {
        if (!_buf.Close()) _state |= FailBit;
    }

    // fidelity: M3-041
    /// <summary>
    /// HandleImuData (0x00535C5E, tag 0xBF). L4: the sequence byte is compared with the whole counter word; a
    /// difference stores it, then logs the directory, builds the name and opens the file with a header. Eight rows follow
    /// on every packet (L12); the final chunk closes (L14/L15).
    /// </summary>
    internal void HandleImuData(IMUDataChunk m)
    {
        if (m.SeqId != _counter)
        {
            _counter = m.SeqId;                                    // 0x00535C64..0x00535C76: before the directory
            string dir = LogsDirectory();
            if (!EngineFileUtils.CreateDirectory(dir))                            // L8: the result must be 1
            {
                Log($"error: Robot.HandleImuData.CreateDirFailed: {dir}");
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();   // continues to build and open the file
            }
            // L9: directory + "/imuLog_" + decimal unsigned counter + ".dat"; no existence check, no prior close.
            string name = dir + "/imuLog_" + LibcxxPrintf.FormatUnsigned(_counter) + ".dat";
            LogInfo("Robot.HandleImuData.OpeningLogFile", name);   // L10, G1: before the open
            Open(name);                                            // L11: mode 0x10 -> "w"
            Insert("aX aY aZ gX gY gZ\n");                         // the 18-byte header, result unchecked
        }
        for (int i = 0; i < 8; ++i)                                // L12/L13: AX AY AZ GX GY GZ, five spaces, LF
        {
            Insert(LibcxxPrintf.FormatFloat(m.AX[i])); Insert(" ");
            Insert(LibcxxPrintf.FormatFloat(m.AY[i])); Insert(" ");
            Insert(LibcxxPrintf.FormatFloat(m.AZ[i])); Insert(" ");
            Insert(LibcxxPrintf.FormatFloat(m.GX[i])); Insert(" ");
            Insert(LibcxxPrintf.FormatFloat(m.GY[i])); Insert(" ");
            Insert(LibcxxPrintf.FormatFloat(m.GZ[i])); Insert("\n");
        }
        if (m.TotalNumChunks - 1 == m.ChunkId)                     // L14: bytes promoted; numChunks 0 gives -1
        {
            LogInfo("Robot.HandleImuData.ClosingLogFile", "");     // L15, G1: empty format; the counter is not reset
            CloseFromHandler();
        }
    }

    // fidelity: M3-041
    /// <summary>
    /// HandleImuRawData (0x00536122, tag 0xC7). L16: order 0 starts a log (directory, a name not yet taken, header);
    /// every packet writes one row (L20/L21); order 2 closes after its row (L22).
    /// </summary>
    internal void HandleImuRawData(IMURawDataChunk m)
    {
        if (m.Order == 0)
        {
            string dir = LogsDirectory();
            if (!EngineFileUtils.CreateDirectory(dir))                            // L17: a zero result
            {
                Log($"error: Robot.HandleImuRawData.CreateDirFailed: {dir}");
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();   // continues with the filename search
            }
            string name;
            do                                                     // L18: ++counter (uint wrap) until the name is free
            {
                unchecked { ++_counter; }
                name = dir + "/imuRawLog_" + LibcxxPrintf.FormatUnsigned(_counter) + ".dat";
            } while (EngineFileUtils.FileExists(name));      // F8.2: stat ok and mode bit 0x8000
            LogInfo("Robot.HandleImuRawData.OpeningLogFile", name);  // L19, G1
            Open(name);
            Insert("timestamp aX aY aZ gX gY gZ\n");               // the 28-byte header
        }
        Insert(LibcxxPrintf.FormatLong(m.Timestamp)); Insert(" "); // L20: the byte through the int insertion
        Insert(LibcxxPrintf.FormatLong(m.A[0])); Insert(" ");      // short insertions go through the signed-long slot
        Insert(LibcxxPrintf.FormatLong(m.A[1])); Insert(" ");
        Insert(LibcxxPrintf.FormatLong(m.A[2])); Insert(" ");
        Insert(LibcxxPrintf.FormatLong(m.G[0])); Insert(" ");
        Insert(LibcxxPrintf.FormatLong(m.G[1])); Insert(" ");
        Insert(LibcxxPrintf.FormatLong(m.G[2])); Insert("\n");
        if (m.Order == 2)
        {
            LogInfo("Robot.HandleImuRawData.ClosingLogFile", "");   // L22, G1
            CloseFromHandler();
        }
    }

    // fidelity: M3-041
    /// <summary>
    /// L35: ~Messaging runs the filebuf destructor after the subscription vector is released (M1-046 owns that
    /// position): close, result ignored, no ClosingLogFile diagnostic, no callback.
    /// </summary>
    internal void Destroy()
    {
        try { _buf.Close(); }
        catch (Exception) { }                                      // 0x005010FC..0x00501100: the destructor catches
    }
}

// fidelity: M3-041
/// <summary>
/// The engine's <c>basic_filebuf&lt;char&gt;</c> put area as the IMU ofstream uses it (filebuf = Messaging+0x44), ported from
/// re-analysis/research/20261010-m3041-filebuf-rows.md F1..F7. Pointers are offsets into the owned 4096-byte buffer; -1 is
/// the null pointer.
/// Phone stdio boundary: the FILE* (fopen "w" / fwrite / fflush / fclose) is a <see cref="FileStream"/>; the phone's own stdio
/// buffering behind it is external. Only the filebuf's put area, which the engine owns, is modelled here.
/// Boundary (F1.2, open question 2, MISSING in the inventory): <c>__always_noconv_</c> is assumed 1 (codecvt&lt;char,char,
/// mbstate_t&gt;), so the not-always-noconv paths (F4.5, the unshift loop of F5.2) are not modelled. The read-mode branch of
/// sync (fseeko, 0x0050144C) is not modelled either; it cannot be reached here because a successful open is always followed
/// by a header insertion, which enters write mode (F1.6).
/// </summary>
internal sealed class EngineFilebuf
{
    private const int Eof = -1;
    private const int Ebs = 4096;                                  // F1.3/F1.4: setbuf(null, 4096): owned, heap-allocated
    private const int WriteModeBit = 0x10;                         // __cm_ bit 0x10 (F1.6)

    private readonly byte[] _extbuf = new byte[Ebs];               // F1.4: allocated at construction (__owns_eb_ = 1)
    private int _pbase = -1, _pptr = -1, _epptr = -1;              // F1.5: null until the first write mode
    private int _cm;                                               // __cm_ = 0 at construction (F1.2)
    private FileStream? _file;                                     // FILE* at +0x40; null = closed (F1.2)

    internal bool IsOpen => _file is not null;

    // F1.6 __write_mode (0x005017F8): no-op if cm has 0x10; else pbase = pptr = buf, epptr = buf + ebs - 1, cm = 0x10.
    private void WriteMode()
    {
        if ((_cm & WriteModeBit) != 0) return;
        _pbase = _pptr = 0;
        _epptr = Ebs - 1;
        _cm = WriteModeBit;
    }

    // F4.1..F4.3 overflow(c) (0x00501684).
    private int Overflow(int c)
    {
        if (_file is null) return Eof;                             // F3.1: touches nothing, buffered bytes are kept
        WriteMode();
        int pbase = _pbase, epptr = _epptr;
        int pptr = _pptr;
        if (c != Eof) { _extbuf[pptr] = (byte)c; _pptr = ++pptr; } // F4.1: c lands in the spare byte at epptr
        if (pptr != pbase)
        {
            if (!Fwrite(pbase, pptr - pbase)) return Eof;          // F4.2: short fwrite: -1, no reset
            _pbase = _pptr = pbase;                                // F4.3: pptr = pbase, epptr as read after write mode
            _epptr = epptr;
        }
        return c == Eof ? 0 : c;
    }

    // F2.1 xsputn(s, n) (0x004E3F06): memcpy up to epptr - pptr (no FILE test), else overflow(next byte), stop on -1.
    internal int Xsputn(byte[] s)
    {
        int written = 0, n = s.Length;
        while (written < n)
        {
            if (_pptr < _epptr)
            {
                int k = Math.Min(_epptr - _pptr, n - written);
                Buffer.BlockCopy(s, written, _extbuf, _pptr, k);
                _pptr += k; written += k;
            }
            else
            {
                if (Overflow(s[written]) == Eof) break;
                written += 1;
            }
        }
        return written;
    }

    // F5.2 sync() (0x00501398): FILE null -> 0; in write mode a pending put area goes through overflow(eof) (-1 on
    // failure); then fflush, nonzero -> -1. No reset of cm or the pointers (the read-mode block is not reached).
    // Without the write-mode bit the engine returns 0 at 0x005013B8 with no fflush; unreachable here, since every
    // successful open is followed by a header insertion, which enters write mode, and nothing clears cm.
    private int Sync()
    {
        if (_file is null) return 0;
        if ((_cm & WriteModeBit) != 0 && _pptr != _pbase && Overflow(Eof) == Eof) return -1;
        try { _file.Flush(); }
        catch (Exception e) when (e is IOException or ObjectDisposedException or NotSupportedException) { return -1; }
        return 0;
    }

    // F5.1 close() (0x0050111C): FILE null -> fail; sync, then fclose even when sync failed; the FILE is cleared only when
    // fclose succeeded; success only when sync returned 0.
    internal bool Close()
    {
        if (_file is null) return false;
        int r = Sync();
        try { _file.Dispose(); }
        catch (IOException) { return false; }                      // fclose failed: the FILE pointer stays set
        _file = null;
        return r == 0;
    }

    // F6.1 filebuf::open(name, "w") (0x00538A3C): FILE non-null -> fail, nothing touched; else fopen "w", null -> fail.
    // Never touches cm or the put area (stale bytes buffered while the FILE was null stay, F6.2).
    internal bool Open(string path)
    {
        if (_file is not null) return false;
        try { _file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
        return true;
    }

    // fwrite(buf + from, 1, n, FILE): true when all n bytes were written.
    private bool Fwrite(int from, int n)
    {
        try { _file!.Write(_extbuf, from, n); return true; }
        catch (Exception e) when (e is IOException or ObjectDisposedException or NotSupportedException) { return false; }
    }
}

// fidelity: M3-041
// fidelity: M3-042
/// <summary>
/// FileUtils as the IMU handlers call it (F8.1..F8.7): DirectoryExists 0x00802824, FileExists 0x0080342A,
/// CreateDirectory(path, false, true) 0x00802874.
/// Host mapping (phone libc is external): stat is the .NET attribute query (a path whose attributes cannot be read is a
/// failed stat); the S_IFDIR bit 0x4000 is the Directory attribute; the 0x8000 bit (the Windows CRT's S_IFREG for any
/// non-directory file) is "exists and is not a directory"; mkdir(prefix, 0700) is Directory.CreateDirectory with
/// UnixFileMode 0700 on Unix and the plain call on Windows (no POSIX modes there).
/// </summary>
internal static class EngineFileUtils
{
    private static bool Stat(string path, out bool isDir)
    {
        isDir = false;
        try { isDir = (File.GetAttributes(path) & FileAttributes.Directory) != 0; return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
    }

    // F8.1: stat ok and the 0x4000 bit.
    internal static bool DirectoryExists(string path) => Stat(path, out bool isDir) && isDir;

    // F8.2: stat ok and the 0x8000 bit: a directory is not a file (the single-bit test).
    internal static bool FileExists(string path) => Stat(path, out bool isDir) && !isDir;

    private static bool Mkdir0700(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
            else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
    }

    // F8.3/F8.4/F8.5/F8.7: arg2 false (whole path), arg3 true (prefix walk, 200-iteration cap).
    internal static bool CreateDirectory(string path)
    {
        if (path.Length == 0) return true;                         // F8.4: empty local string: true
        int pos = 0, counter = 1;
        bool r7;
        for (;;)
        {
            pos = path.IndexOf('/', pos + 1);                      // find('/', pos + 1): the slash at index 0 is skipped
            string prefix = pos < 0 ? path : path.Substring(0, pos);
            if (!DirectoryExists(prefix) && !Mkdir0700(prefix)) return false;
            r7 = counter < 200;                                    // F8.5: reaching counter 200 is false
            if (counter > 199) return r7;
            counter++;
            if (pos < 0) return r7;                                // npos: the last component was done
        }
    }
}
