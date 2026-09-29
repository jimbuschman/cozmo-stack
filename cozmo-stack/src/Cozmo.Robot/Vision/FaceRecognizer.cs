using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

// ---------------------------------------------------------------------------------------------------
// The OKAO third-party boundary (M14-011, Correction C3).  The rows name these OKAO_FR_* operations:
// GetRegisteredUserNum, Identify, RegisterData, GetRegisteredUsrDataNum, ClearUser, Verify,
// GetFeatureFromAlbum, ClearData, IsRegistered, RestoreAlbum, GetAlbumMaxNum, ClearAlbum and
// DeleteAlbumHandle.  Everything Anki-side above them is the state machine below; everything inside
// them is Omron's library and is not reproduced here, exactly as OkaoFaceDetector is the M14-010 seam.
//
// The opaque types stand for the C++ album/common/feature handles the native calls pass by pointer.
// ---------------------------------------------------------------------------------------------------

/// <summary>An opaque OKAO face feature (the native recognition feature blob).</summary>
public sealed class OkaoFaceFeature { }

/// <summary>An opaque OKAO album handle.</summary>
public sealed class OkaoAlbumHandle { }

/// <summary>An opaque OKAO common handle (the per-process OKAO context).</summary>
public sealed class OkaoCommonHandle { }

/// <summary>
/// The OKAO face-recognition boundary (<c>OKAO_FR_*</c>).  The Anki-side state machine is
/// <see cref="FaceRecognizer"/>; this interface is the library underneath it.  The stock implementation
/// reports itself unavailable.
/// </summary>
public interface IOkaoFaceRecognizer
{
    bool IsAvailable { get; }
    string Description { get; }

    /// <summary>The OKAO common handle the recognizer holds.</summary>
    OkaoCommonHandle CreateCommon();
    /// <summary>The album the recognizer works on in production.</summary>
    OkaoAlbumHandle CreateAlbum();

    /// <summary><c>OKAO_FR_GetRegisteredUserNum(album, &amp;count)</c>: 0 on success.</summary>
    int GetRegisteredUserNum(OkaoAlbumHandle album, out int count);

    /// <summary><c>OKAO_FR_Identify(feature, album, 10, ids, scores, &amp;count)</c>: 0 on success.</summary>
    int Identify(OkaoFaceFeature feature, OkaoAlbumHandle album, int maxResults, int[] ids, int[] scores, out int count);

    /// <summary><c>OKAO_FR_RegisterData(album, feature, albumEntry, dataIndex)</c>: 0 on success.</summary>
    int RegisterData(OkaoAlbumHandle album, OkaoFaceFeature feature, int albumEntry, int dataIndex);

    /// <summary><c>OKAO_FR_GetRegisteredUsrDataNum(album, albumEntry, &amp;count)</c>: 0 on success.</summary>
    int GetRegisteredUsrDataNum(OkaoAlbumHandle album, int albumEntry, out int count);

    /// <summary><c>OKAO_FR_ClearUser(album, albumEntry)</c>: 0 on success.</summary>
    int ClearUser(OkaoAlbumHandle album, int albumEntry);

    /// <summary>Removes one feature of an album entry (C3-14's temporary removal): 0 on success.</summary>
    int ClearData(OkaoAlbumHandle album, int albumEntry, int dataIndex);

    /// <summary><c>OKAO_FR_Verify(feature, album, albumEntry, &amp;score)</c> (C4-8): 0 on success.</summary>
    int Verify(OkaoFaceFeature feature, OkaoAlbumHandle album, int albumEntry, out int score);

    /// <summary>Reads one stored feature back (C3-14): 0 on success.</summary>
    int GetFeatureFromAlbum(OkaoAlbumHandle album, int albumEntry, int dataIndex, out OkaoFaceFeature feature);

    /// <summary><c>OKAO_FR_IsRegistered(album, albumEntry, dataIndex, &amp;result)</c> (C4-8): 0 on success; a nonzero result means registered.</summary>
    int IsRegistered(OkaoAlbumHandle album, int albumEntry, int dataIndex, out int result);

    /// <summary><c>OKAO_FR_RestoreAlbum(common, data, size, &amp;result)</c> (C4-8): the restored handle is the return; <paramref name="result"/> is the status.</summary>
    OkaoAlbumHandle? RestoreAlbum(OkaoCommonHandle common, byte[] data, int size, out int result);

    /// <summary><c>OKAO_FR_GetAlbumMaxNum(album, &amp;albumMax, &amp;dataMax)</c> (C4-8): 0 on success.</summary>
    int GetAlbumMaxNum(OkaoAlbumHandle album, out int albumMax, out int dataMax);

    /// <summary><c>OKAO_FR_ClearAlbum</c>: 0 on success.</summary>
    int ClearAlbum(OkaoAlbumHandle album);

    /// <summary><c>OKAO_FR_DeleteAlbumHandle</c>: 0 on success.</summary>
    int DeleteAlbumHandle(OkaoAlbumHandle album);
}

/// <summary>
/// The stock OKAO recognizer: the Omron library is proprietary and not available in this stack, so
/// every operation is an explicit boundary rather than a silent default.
/// </summary>
public sealed class OkaoFaceRecognizer : IOkaoFaceRecognizer
{
    public bool IsAvailable => false;
    public string Description => "Omron OKAO Vision face recognition (FaceRecognizer over the OKAO_FR_* exports): proprietary, not available in this stack";

    public OkaoCommonHandle CreateCommon() => throw new NotSupportedException("OKAO face recognition is not available in this stack");
    public OkaoAlbumHandle CreateAlbum() => throw new NotSupportedException("OKAO face recognition is not available in this stack");
    public int GetRegisteredUserNum(OkaoAlbumHandle album, out int count) => throw new NotSupportedException("OKAO_FR_GetRegisteredUserNum");
    public int Identify(OkaoFaceFeature feature, OkaoAlbumHandle album, int maxResults, int[] ids, int[] scores, out int count) => throw new NotSupportedException("OKAO_FR_Identify");
    public int RegisterData(OkaoAlbumHandle album, OkaoFaceFeature feature, int albumEntry, int dataIndex) => throw new NotSupportedException("OKAO_FR_RegisterData");
    public int GetRegisteredUsrDataNum(OkaoAlbumHandle album, int albumEntry, out int count) => throw new NotSupportedException("OKAO_FR_GetRegisteredUsrDataNum");
    public int ClearUser(OkaoAlbumHandle album, int albumEntry) => throw new NotSupportedException("OKAO_FR_ClearUser");
    public int ClearData(OkaoAlbumHandle album, int albumEntry, int dataIndex) => throw new NotSupportedException("OKAO_FR_ClearData");
    public int Verify(OkaoFaceFeature feature, OkaoAlbumHandle album, int albumEntry, out int score) => throw new NotSupportedException("OKAO_FR_Verify");
    public int GetFeatureFromAlbum(OkaoAlbumHandle album, int albumEntry, int dataIndex, out OkaoFaceFeature feature) => throw new NotSupportedException("OKAO_FR_GetFeatureFromAlbum");
    public int IsRegistered(OkaoAlbumHandle album, int albumEntry, int dataIndex, out int result) => throw new NotSupportedException("OKAO_FR_IsRegistered");
    public OkaoAlbumHandle? RestoreAlbum(OkaoCommonHandle common, byte[] data, int size, out int result) => throw new NotSupportedException("OKAO_FR_RestoreAlbum");
    public int GetAlbumMaxNum(OkaoAlbumHandle album, out int albumMax, out int dataMax) => throw new NotSupportedException("OKAO_FR_GetAlbumMaxNum");
    public int ClearAlbum(OkaoAlbumHandle album) => throw new NotSupportedException("OKAO_FR_ClearAlbum");
    public int DeleteAlbumHandle(OkaoAlbumHandle album) => throw new NotSupportedException("OKAO_FR_DeleteAlbumHandle");
}

/// <summary>
/// One album entry owned by an <see cref="EnrolledFaceEntry"/> (the per-album-entry last-seen time and
/// the session-only flag C3-9/C3-15/C3-17 name).
/// </summary>
public sealed class EnrolledFaceAlbumEntry
{
    public int AlbumEntryId { get; set; }
    /// <summary>The entry's last-seen time in microseconds (<c>+0x30</c> per-entry counterpart).</summary>
    public long LastSeenMicros { get; set; }
    /// <summary>Whether this is the session-only (current) entry (C3-9's <c>isSessionOnly=true</c>).</summary>
    public bool SessionOnly { get; set; }
}

/// <summary>
/// The engine's <c>EnrolledFaceEntry</c>.  Field names follow the C3 rows; the field the rows call
/// <c>+0x04</c> has no established source-level name, so it is kept as <see cref="Offset04"/> and must
/// not be given a guessed semantic name (C3-18).
/// </summary>
public sealed class EnrolledFaceEntry
{
    /// <summary>The face id (<c>faceID</c>).</summary>
    public int FaceID { get; set; }
    /// <summary>The known name; empty for a session-only face.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// <c>EnrolledFaceEntry +0x04</c>.  Its merge rule is exact (C3-18: copied from the other entry only
    /// when it equals that entry's face id), but its source-level name is UNKNOWN, so it stays by offset.
    /// </summary>
    public int Offset04 { get; set; }
    /// <summary>The maximum score (<c>+0x1C</c>).</summary>
    public int Score { get; set; }
    /// <summary>The current track id (<c>+0x20</c>; set from recognizer <c>+0xAC</c>, C3-9/C3-12).</summary>
    public int CurrentTrack { get; set; }
    /// <summary>The previous track id (<c>+0x24</c>; shifted by C3-12).</summary>
    public int PreviousTrack { get; set; }
    /// <summary>The earlier timestamp (<c>+0x28</c>), microseconds.</summary>
    public long Timestamp28Micros { get; set; }
    /// <summary>The later timestamp (<c>+0x30</c>), microseconds.</summary>
    public long Timestamp30Micros { get; set; }

    /// <summary>The owned album entries, keyed by album-entry id (the C3-19 "last-seen map").</summary>
    public Dictionary<int, EnrolledFaceAlbumEntry> AlbumEntries { get; } = new();
    /// <summary>The current/session album entry, or -1 (C3-19 emits it first).</summary>
    public int CurrentAlbumEntryId { get; set; } = -1;
    /// <summary>The debug matches installed by C3-4 (the top match plus unique later ids, at most two).</summary>
    public List<DebugMatch> DebugMatches { get; } = new();
}

/// <summary>One debug match (C3-4/C5-2): the face id (0 on a miss), its score, and the record's name (empty when missing).</summary>
public sealed record DebugMatch(int FaceID, int Score, string Name);

/// <summary>
/// The CLAD <c>EnrolledFaceStorage</c> the rows describe (C3-19/C3-20).  The field order and offsets are
/// the row's: two seconds timestamps, a parallel pair of album-entry timestamp/id vectors, the face id
/// and the name.
/// </summary>
public sealed class StoredEnrolledFace
{
    public long Timestamp28Seconds { get; set; }
    public long Timestamp30Seconds { get; set; }
    public List<long> AlbumEntrySeconds { get; } = new();
    public int FaceID { get; set; }
    public List<int> AlbumEntryIds { get; } = new();
    public string Name { get; set; } = string.Empty;

    // The enrollment-data container the rows describe (C3-21): an 8-byte header whose first four bytes
    // are the 0x0002FACE version prefix and whose next four are nextFaceID, then packed records.
    public const uint VersionPrefix = 0x0002FACE;
    public const int HeaderBytes = 8;

    /// <summary>
    /// Packs one record (C4-1): no padding; <c>+0x00</c> int64 LE ts28; <c>+0x08</c> int64 LE ts30; the
    /// timestamps vector as a <b>1-byte</b> count then n1 int64 LE; int32 LE face ID; the ids vector as a
    /// <b>1-byte</b> count then n2 int32 LE; the name as a <b>1-byte</b> length then that many raw bytes.
    /// </summary>
    public void Pack(List<byte> b)
    {
        WriteI64(b, Timestamp28Seconds);
        WriteI64(b, Timestamp30Seconds);
        b.Add((byte)AlbumEntrySeconds.Count);
        foreach (var s in AlbumEntrySeconds) WriteI64(b, s);
        WriteI32(b, FaceID);
        b.Add((byte)AlbumEntryIds.Count);
        foreach (var id in AlbumEntryIds) WriteI32(b, id);
        var name = System.Text.Encoding.Latin1.GetBytes(Name);     // N4: 1:1 raw bytes
        b.Add((byte)name.Length);
        b.AddRange(name);
    }

    /// <summary>The packed size of one record (C4-1): 23 + 8*n1 + 4*n2 + len.</summary>
    public int Size() => 23 + 8 * AlbumEntrySeconds.Count + 4 * AlbumEntryIds.Count
                         + System.Text.Encoding.Latin1.GetByteCount(Name);

    /// <summary>
    /// Reads one packed record (C4-1).  Null on a malformed record (insufficient bytes, an over-long
    /// count, or a consumed length that does not equal <see cref="Size"/>), which C3-21/C4-1 turns into
    /// a load failure.
    /// </summary>
    public static StoredEnrolledFace? Unpack(byte[] data, ref int pos)
    {
        int start = pos;
        if (!TryReadI64(data, ref pos, out long ts28)) return null;
        if (!TryReadI64(data, ref pos, out long ts30)) return null;
        if (pos + 1 > data.Length) return null;
        int timeCount = data[pos++];
        if (timeCount > (data.Length - pos) / 8) return null;
        var times = new List<long>(timeCount);
        for (int i = 0; i < timeCount; i++) { if (!TryReadI64(data, ref pos, out long t)) return null; times.Add(t); }
        if (!TryReadI32(data, ref pos, out int faceId)) return null;
        if (pos + 1 > data.Length) return null;
        int idCount = data[pos++];
        if (idCount > (data.Length - pos) / 4) return null;
        var ids = new List<int>(idCount);
        for (int i = 0; i < idCount; i++) { if (!TryReadI32(data, ref pos, out int id)) return null; ids.Add(id); }
        if (pos + 1 > data.Length) return null;
        int nameLen = data[pos++];
        if (nameLen > data.Length - pos) return null;
        var name = System.Text.Encoding.Latin1.GetString(data, pos, nameLen); pos += nameLen;   // N4
        var s = new StoredEnrolledFace { Timestamp28Seconds = ts28, Timestamp30Seconds = ts30, FaceID = faceId, Name = name };
        s.AlbumEntrySeconds.AddRange(times);
        s.AlbumEntryIds.AddRange(ids);
        if (pos - start != s.Size()) return null;
        return s;
    }

    private static void WriteI32(List<byte> b, int v) => b.AddRange(BitConverter.GetBytes(v));
    private static void WriteI64(List<byte> b, long v) => b.AddRange(BitConverter.GetBytes(v));
    private static bool TryReadI32(byte[] d, ref int p, out int v) { if (p + 4 > d.Length) { v = 0; return false; } v = BitConverter.ToInt32(d, p); p += 4; return true; }
    private static bool TryReadI64(byte[] d, ref int p, out long v) { if (p + 8 > d.Length) { v = 0; return false; } v = BitConverter.ToInt64(d, p); p += 8; return true; }
}

/// <summary>
/// The engine's <c>FaceRecognizer</c> (M14-011, Correction C3).  Every row of C3 is implemented here:
/// <list type="bullet">
/// <item><c>RecognizeFace</c> C3-1..C3-8: the confident &gt;750 gate and top mapping, debug-match install,
/// the session-only lower-ranked 675/<c>min(score-75,600)</c> path, the ordinary update path, the
/// below-gate 550 new-user path and the active-target path;</item>
/// <item><c>RegisterNewUser</c> C3-9, <c>GetNextAlbumEntryToUse</c> C3-10, <c>GetFaceIDforAlbumEntry</c> C3-11;</item>
/// <item><c>UpdateExistingAlbumEntry</c> C3-12..C3-15 with the four-entry replacement;</item>
/// <item>both <c>RemoveUser</c> overloads C3-16, <c>MergeFaces</c>/<c>MergeWith</c> C3-17..C3-18;</item>
/// <item><c>ConvertToEnrolledFaceStorage</c> and its inverse C3-19..C3-20, the enrollment deserialize
/// C3-21, the album install C3-22..C3-23 and the <c>SetSerializedData</c> chain endpoint C3-24.</item>
/// </list>
/// The OKAO_FR_* calls are the third-party boundary (<see cref="IOkaoFaceRecognizer"/>).  The stock seam
/// reports itself unavailable, so nothing here runs on the shipped stack.
/// </summary>
// fidelity: M14-011
public sealed class FaceRecognizer
{
    public const int MaxAlbumEntries = 1000;
    public const int MaxResults = 10;
    /// <summary>The confident gate: the top score must be strictly greater than this (C3-3, 0x2EF = 751).</summary>
    public const int ConfidentScore = 750;
    /// <summary>The new-user ceiling below the confident gate: no results or top &lt; 550 (C3-7, 0x226).</summary>
    public const int NewUserScoreCeiling = 550;
    /// <summary>The lower-ranked named-candidate threshold: strictly over this (C3-5, 0x2A4 = 676).</summary>
    public const int LowerRankedThreshold = 675;
    /// <summary>The four-entry data limit (C3-14).</summary>
    public const int MaxDataEntries = 4;
    /// <summary>The merge limit M14's caller passes (C3-17, 0x008639C0).</summary>
    public const int MergeLimit = 5;
    /// <summary>The one-second enrollment gate constant (C3-13, 0x000F423F = 999,999).</summary>
    public const long EnrollmentGateMicros = 999_999;
    /// <summary>The score a freshly registered user reports (C3-1/C3-7, 0x3E8 = 1000).</summary>
    public const int RegisteredUserScore = 1000;
    private const long MicrosPerSecond = 1_000_000;

    private readonly IOkaoFaceRecognizer _okao;
    private readonly Func<long> _nowMicros;
    private readonly object _gate = new();

    private readonly OkaoCommonHandle? _common;
    private OkaoAlbumHandle? _album;

    /// <summary>+0xE8: album-entry id to face id.</summary>
    private readonly Dictionary<int, int> _albumEntryToFaceId = new();
    /// <summary>The enrolled-face map.</summary>
    private readonly Dictionary<int, EnrolledFaceEntry> _faces = new();
    /// <summary>The track LUT (track id to face id).</summary>
    private readonly Dictionary<int, int> _trackToFace = new();

    private int _nextFaceId = 1;
    private int _nextAlbumProbe;

    public FaceRecognizer(IOkaoFaceRecognizer okao, Func<long>? nowMicros = null)
    {
        _okao = okao;
        _nowMicros = nowMicros ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000);
        if (okao.IsAvailable)
        {
            _common = okao.CreateCommon();
            _album = okao.CreateAlbum();
        }
    }

    public event Action<string>? Log;
    /// <summary>The loaded faces reported at the end of <see cref="SetSerializedData"/> (C3-24).</summary>
    public event Action<IReadOnlyList<(int FaceID, string Name)>>? FacesLoaded;

    public bool IsAvailable => _okao.IsAvailable;

    /// <summary>The current track id (recognizer <c>+0xAC</c>).</summary>
    public int CurrentTrack { get; set; }
    /// <summary>Whether per-frame registration is allowed (recognizer <c>+0xFC</c>).</summary>
    public bool RegistrationAllowed { get; set; }
    /// <summary>The enrollment target id (recognizer <c>+0x100</c>).</summary>
    public int EnrollmentTargetId { get; private set; }
    /// <summary>The enrollment track (recognizer <c>+0x104</c>).</summary>
    public int EnrollmentTrack { get; private set; }
    /// <summary>The remaining enrollment count (recognizer <c>+0x108</c>).</summary>
    public int EnrollmentCount { get; private set; }
    /// <summary>The configured enrollment mode (recognizer <c>+0x10c</c>).</summary>
    public int EnrollmentMode { get; private set; }
    /// <summary>The cancel flag (recognizer <c>+0x5D</c>).</summary>
    public bool EnrollmentCancelled { get; private set; }

    /// <summary>The next face id handed out (set from the enrollment header, C3-21/C3-24).</summary>
    public int NextFaceID { get { lock (_gate) return _nextFaceId; } }

    /// <summary>The enrolled faces (diagnostics/tests).  The entries are live references.</summary>
    public IReadOnlyList<EnrolledFaceEntry> EnrolledFaces { get { lock (_gate) return _faces.Values.ToList(); } }

    /// <summary>The current production album handle (tests and diagnostics).</summary>
    public OkaoAlbumHandle? AlbumHandle => _album;

    private long Now => _nowMicros();

    /// <summary>
    /// <c>FaceRecognizer::SetAllowedEnrollments(mode, id)</c> 0x008658AC (C1-F11): a zero id cancels an
    /// in-flight enrollment (setting +0x5D when the target is nonzero and the count positive) and clears
    /// the target/track; a nonzero id stores the mode at +0x108/+0x10c, the id at +0x100 and the enrolled
    /// entry's current track (+0x20) at +0x104, or 0 with a warning when there is no record.
    /// </summary>
    // fidelity: M14-011
    public void SetAllowedEnrollments(int mode, int id)
    {
        lock (_gate)
        {
            if (id == 0)
            {
                if (EnrollmentTargetId != 0 && EnrollmentCount > 0)
                {
                    Log?.Invoke("SetAllowedEnrollments: cancelling the in-flight enrollment");
                    EnrollmentCancelled = true;
                }
                EnrollmentCount = mode;
                EnrollmentMode = mode;
                EnrollmentTargetId = 0;
                EnrollmentTrack = 0;
                return;
            }
            EnrollmentCount = mode;
            EnrollmentMode = mode;
            EnrollmentTargetId = id;
            if (_faces.TryGetValue(id, out var entry)) EnrollmentTrack = entry.CurrentTrack;
            else { EnrollmentTrack = 0; Log?.Invoke($"SetAllowedEnrollments: No data for enrollmentID={id}"); }
        }
    }

    /// <summary>
    /// <c>GetNextFaceID</c> 0x0086561C (C4-2/C5-6): load <c>id = this+0xf4</c>; if it is free return it
    /// without storing; otherwise increment (wrap 0 to 1), store to <c>+0xf4</c>, and repeat while the id
    /// is still present.  After the call <c>+0xf4</c> equals the returned id.
    /// </summary>
    // fidelity: M14-011
    public int GetNextFaceID()
    {
        lock (_gate)
        {
            int id = _nextFaceId;
            if (!_faces.ContainsKey(id)) return id;                // free: no store
            do
            {
                id++;
                if (id == 0) id = 1;
                _nextFaceId = id;
            } while (_faces.ContainsKey(id));
            return id;
        }
    }

    /// <summary>
    /// <c>GetFaceIDforAlbumEntry</c> 0x00866C1C (C3-11): the album-entry-to-face-id map at +0xE8; a miss
    /// logs and returns 0.
    /// </summary>
    // fidelity: M14-011
    public int GetFaceIDforAlbumEntry(int albumEntry)
    {
        lock (_gate)
        {
            if (_albumEntryToFaceId.TryGetValue(albumEntry, out int faceId)) return faceId;
            Log?.Invoke($"GetFaceIDforAlbumEntry: no face id for album entry {albumEntry}");
            return 0;
        }
    }

    /// <summary>
    /// <c>GetNextAlbumEntryToUse</c> 0x00865178 (C3-10/C4-6): the free-index probe starts at
    /// <c>this+0xf8</c> (0), runs only while the OKAO registered-user count is below 1000, consults
    /// <b>OKAO only</b> (<c>OKAO_FR_IsRegistered</c>, data index 0), advances index+1 and wraps to 0
    /// after 998, for at most 1000 iterations; a nonzero OKAO return is an error (-1).  When the album is
    /// full it selects the oldest <b>unnamed</b> entry by <c>+0x30</c>, removes it, reuses its current
    /// album entry and verifies it is no longer registered.
    /// </summary>
    // fidelity: M14-011
    public int GetNextAlbumEntryToUse()
    {
        lock (_gate)
        {
            if (_album is null) return -1;
            if (_okao.GetRegisteredUserNum(_album, out int registered) != 0) return -1;
            if (registered < MaxAlbumEntries)
            {
                int index = _nextAlbumProbe;
                for (int i = 0; i < MaxAlbumEntries; i++)
                {
                    if (_okao.IsRegistered(_album, index, 0, out int result) != 0)
                    {
                        Log?.Invoke($"GetNextAlbumEntryToUse: OKAO_FR_IsRegistered failed for album entry {index}");
                        return -1;
                    }
                    if (result == 0) return index;                 // free: leave _nextAlbumProbe at it
                    _nextAlbumProbe = index > 998 ? 0 : index + 1; // registered: advance and store
                    index = _nextAlbumProbe;
                }
                Log?.Invoke("GetNextAlbumEntryToUse: 1000 probes found no free album entry");
                return -1;
            }
            EnrolledFaceEntry? oldest = null;
            EnrolledFaceAlbumEntry? oldestAlbum = null;
            foreach (var f in _faces.Values)
            {
                if (f.Name.Length != 0) continue;                      // unnamed only (C4-6)
                if (f.CurrentAlbumEntryId < 0) continue;
                if (!f.AlbumEntries.TryGetValue(f.CurrentAlbumEntryId, out var a)) continue;
                if (oldest is null || f.Timestamp30Micros < oldest.Timestamp30Micros) { oldest = f; oldestAlbum = a; }
            }
            if (oldest is null || oldestAlbum is null) return -1;
            int entry = oldestAlbum.AlbumEntryId;
            RemoveUser(oldest.FaceID);
            if (_okao.IsRegistered(_album, entry, 0, out int still) != 0) return -1;
            return still == 0 ? entry : -1;
        }
    }

    /// <summary>
    /// <c>RegisterNewUser</c> 0x00865658 (C3-9): allocate an album entry and face id, register the
    /// feature, create the session-only enrolled entry, map the album entry, insert the record and
    /// decrement the remaining count.  0 on success, 1 otherwise.
    /// </summary>
    // fidelity: M14-011
    public int RegisterNewUser(OkaoFaceFeature feature, out int faceID)
    {
        faceID = 0;
        lock (_gate)
        {
            if (_album is null) return 1;
            int albumEntry = GetNextAlbumEntryToUse();
            if (albumEntry < 0) return 1;
            int newFaceId = GetNextFaceID();
            if (_okao.RegisterData(_album, feature, albumEntry, 0) != 0) return 1;
            long now = Now;
            var entry = new EnrolledFaceEntry { FaceID = newFaceId, Timestamp28Micros = now, Timestamp30Micros = now, CurrentTrack = CurrentTrack };
            entry.AlbumEntries[albumEntry] = new EnrolledFaceAlbumEntry { AlbumEntryId = albumEntry, LastSeenMicros = now, SessionOnly = true };
            entry.CurrentAlbumEntryId = albumEntry;
            _albumEntryToFaceId[albumEntry] = newFaceId;
            _faces[newFaceId] = entry;
            if (EnrollmentCount > 0) EnrollmentCount--;
            faceID = newFaceId;
            return 0;
        }
    }

    /// <summary>
    /// <c>RecognizeFace</c> 0x008640E4 (C3-1..C3-8).  Returns 0 on success (possibly with no face) and 1
    /// on a failure the engine propagates.
    /// </summary>
    // fidelity: M14-011
    public int RecognizeFace(OkaoFaceFeature feature, out int faceID, out int score)
    {
        faceID = 0; score = 0;
        lock (_gate)
        {
            if (_album is null) return 1;
            // C3-1
            if (_okao.GetRegisteredUserNum(_album, out int registeredCount) != 0) return 1;
            // C5-1: the empty-album first-user branch tests the enrollment target id +0x100, not the track.
            if (EnrollmentCount != 0 && registeredCount == 0 && EnrollmentTargetId == 0)
            {
                if (RegisterNewUser(feature, out faceID) != 0) return 1;
                score = RegisteredUserScore;
                return 0;
            }

            // C3-2: Identify, bounded to ten.
            var ids = new int[MaxResults];
            var scores = new int[MaxResults];
            if (_okao.Identify(feature, _album, MaxResults, ids, scores, out int n) != 0)
            {
                Log?.Invoke("RecognizeFace: OKAO_FR_Identify failed");
                return 0;
            }
            if (n > MaxResults)
            {
                Log?.Invoke($"RecognizeFace: Identify returned {n} results, more than the loop bound {MaxResults}; clamping");
                n = MaxResults;
            }

            // C3-3 confident gate.
            if (n >= 1 && scores[0] > ConfidentScore)
            {
                int topId = GetFaceIDforAlbumEntry(ids[0]);
                if (topId == 0) return 1;
                if (!_faces.TryGetValue(topId, out var topEntry)) return 1;

                // C3-4 debug matches.
                InstallDebugMatches(topEntry, ids, scores, n);

                // C3-5: the top is session-only, there is more than one result and it is not the target.
                if (topEntry.Name.Length == 0 && n > 1 && topId != EnrollmentTargetId)
                {
                    int lower = FindLowerRankedCandidate(ids, scores, n);
                    if (lower >= 0)
                    {
                        int namedId = GetFaceIDforAlbumEntry(ids[lower]);
                        if (namedId != 0 && _faces.ContainsKey(namedId))
                        {
                            Log?.Invoke("RecognizeFace.UsingLowerRankedMatch");
                            MergeFaces(namedId, topId);
                            faceID = namedId; score = scores[lower];
                            if (UpdateExistingAlbumEntry(ids[lower], feature) != 0)
                                Log?.Invoke("RecognizeFace: UpdateExistingAlbumEntry failed on the lower-ranked path");
                            ReplaceEnrollmentTrackIfTarget(namedId);
                            return 0;
                        }
                    }
                }

                // C3-6 ordinary path.
                faceID = topId; score = scores[0];
                if (UpdateExistingAlbumEntry(ids[0], feature) != 0)
                    Log?.Invoke("RecognizeFace: UpdateExistingAlbumEntry failed on the confident path");
                ReplaceEnrollmentTrackIfTarget(topId);
                return 0;
            }

            // C4-3: the low-confidence entry gates on +0x108 != 0 and +0xFC != 0 before anything else;
            // either zero returns success without a face.
            if (EnrollmentCount == 0) return 0;
            if (!RegistrationAllowed) return 0;

            // C3-8/C4-3 active named target, below the confident gate.
            if (EnrollmentTargetId != 0 && CurrentTrack == EnrollmentTrack)
            {
                for (int i = 0; i < n; i++)
                {
                    if (GetFaceIDforAlbumEntry(ids[i]) != EnrollmentTargetId) continue;
                    score = scores[i];
                    if (UpdateExistingAlbumEntry(ids[i], feature) != 0) return 1;
                    break;
                }
                faceID = EnrollmentTargetId;
                return 0;
            }

            // C3-7 below-gate new user.
            if (EnrollmentTargetId == 0 && (n == 0 || scores[0] < NewUserScoreCeiling))
            {
                if (RegisterNewUser(feature, out faceID) == 0)
                {
                    score = RegisteredUserScore;
                    return 0;
                }
            }
            return 0;
        }
    }

    private void ReplaceEnrollmentTrackIfTarget(int faceId)
    {
        if (faceId == EnrollmentTargetId && EnrollmentTrack != CurrentTrack) EnrollmentTrack = CurrentTrack;
    }

    // C3-4/C5-2: the top match, then at most one later candidate; a missing record still gets an empty-name entry.
    private void InstallDebugMatches(EnrolledFaceEntry entry, int[] ids, int[] scores, int n)
    {
        entry.DebugMatches.Clear();
        entry.DebugMatches.Add(new DebugMatch(entry.FaceID, scores[0], entry.Name));
        for (int i = 1; i < n && i < MaxResults && entry.DebugMatches.Count < 2; i++)
        {
            int id = GetFaceIDforAlbumEntry(ids[i]);
            if (id == entry.DebugMatches[0].FaceID) continue;      // skip the top match's id only
            string name = id != 0 && _faces.TryGetValue(id, out var later) ? later.Name : string.Empty;
            entry.DebugMatches.Add(new DebugMatch(id, scores[i], name));
        }
    }

    // C3-5/C5-3: index 1 is the candidate; later candidates are only a conflict guard.
    private int FindLowerRankedCandidate(int[] ids, int[] scores, int n)
    {
        if (n <= 1) return -1;
        int faceId1 = GetFaceIDforAlbumEntry(ids[1]);
        if (faceId1 == 0 || !_faces.TryGetValue(faceId1, out var first))
        {
            Log?.Invoke("RecognizeFace: Missing2ndMatchEnrollmentData");
            return -1;
        }
        if (first.Name.Length == 0) return -1;                     // unnamed first candidate -> ordinary path
        if (scores[1] < 676) return -1;                            // 0x2A4; below -> ordinary path
        int selected = 1;
        long threshold = Math.Min(scores[1] - 75L, 600L);          // computed once
        for (int j = 2; j < n; j++)
        {
            if (scores[j] <= threshold) break;                     // guard only while scores[j] > threshold
            int jd = GetFaceIDforAlbumEntry(ids[j]);
            if (jd == faceId1) continue;                           // same face id -> skip
            if (jd == 0 || !_faces.ContainsKey(jd))
            {
                Log?.Invoke("RecognizeFace: Missing3rdMatchEnrollmentData");
                continue;
            }
            return -1;                                             // a different found record -> ordinary path
        }
        return selected;
    }

    /// <summary>
    /// <c>UpdateExistingAlbumEntry</c> 0x00865A98 (C3-12..C3-15/C4-4): <c>SetAlbumEntryLastSeenTime</c>
    /// writes the album-entry map node; the enrollment gate reads the <b>pre-update</b>
    /// <c>EnrolledFaceEntry+0x30</c>; the four-entry replacement runs; then the success/full continuation
    /// writes <c>entry.Timestamp30Micros = now</c> and the session flag.
    /// </summary>
    // fidelity: M14-011
    public int UpdateExistingAlbumEntry(int albumEntry, OkaoFaceFeature feature)
    {
        lock (_gate)
        {
            if (_album is null) return 1;
            int faceId = GetFaceIDforAlbumEntry(albumEntry);
            if (faceId == 0 || !_faces.TryGetValue(faceId, out var entry)) return 1;
            if (!entry.AlbumEntries.TryGetValue(albumEntry, out var album)) return 1;

            long now = Now;
            album.LastSeenMicros = now;                                            // C3-12 SetAlbumEntryLastSeenTime
            if (entry.CurrentTrack != CurrentTrack) { entry.PreviousTrack = entry.CurrentTrack; entry.CurrentTrack = CurrentTrack; }

            if (_okao.GetRegisteredUsrDataNum(_album, albumEntry, out int dataCount) != 0) return 1;   // C3-12

            // C3-13/C4-4 enable rule: the gate reads the pre-update EnrolledFaceEntry+0x30.
            bool enabled = EnrollmentCount != 0
                           && (EnrollmentTargetId == 0 || EnrollmentTargetId == faceId)
                           && now - entry.Timestamp30Micros > EnrollmentGateMicros
                           && (dataCount < MaxDataEntries || entry.Name.Length == 0 || EnrollmentTargetId == faceId);
            if (enabled && AddEnrollmentData(entry, albumEntry, faceId, feature, dataCount) != 0) return 1;

            // C3-15/C4-4 success/full continuation.
            entry.Timestamp30Micros = now;
            album.SessionOnly = EnrollmentTargetId != faceId;
            if (EnrollmentCount > 0) EnrollmentCount--;
            return 0;
        }
    }

    // C3-14: register under four; with four, held-out verification and lowest-score replacement.
    private int AddEnrollmentData(EnrolledFaceEntry entry, int albumEntry, int faceId, OkaoFaceFeature feature, int dataCount)
    {
        if (dataCount < MaxDataEntries)
            return _okao.RegisterData(_album!, feature, albumEntry, dataCount);
        if (entry.Name.Length != 0 && EnrollmentTargetId != faceId) return 0;
        if (_okao.Verify(feature, _album!, albumEntry, out int fullScore) != 0) return 1;
        int bestIndex = -1, bestScore = int.MaxValue;
        for (int i = 0; i < MaxDataEntries; i++)
        {
            if (_okao.GetFeatureFromAlbum(_album!, albumEntry, i, out var held) != 0) return 1;
            if (_okao.ClearData(_album!, albumEntry, i) != 0) return 1;
            if (_okao.Verify(held, _album!, albumEntry, out int s) != 0) return 1;
            if (_okao.RegisterData(_album!, held, albumEntry, i) != 0) return 1;
            if (s < bestScore) { bestScore = s; bestIndex = i; }
        }
        if (bestIndex >= 0 && bestScore < fullScore)
        {
            // N5: register the new feature directly over the restored held-out slot.
            if (_okao.RegisterData(_album!, feature, albumEntry, bestIndex) != 0) return 1;
        }
        return 0;
    }

    /// <summary>
    /// <c>RemoveUser(faceID)</c> 0x00865544 (C3-16): a hit delegates to the iterator overload; a missing
    /// face logs "UserDoesNotExist"; both return 0.
    /// </summary>
    // fidelity: M14-011
    public int RemoveUser(int faceID)
    {
        lock (_gate)
        {
            if (_faces.TryGetValue(faceID, out var entry)) RemoveUserCore(entry);
            else Log?.Invoke($"RemoveUser: UserDoesNotExist {faceID}");
            return 0;
        }
    }

    /// <summary>The iterator overload 0x00866AE0 (C3-16): erase the track mapping, clear each owned album entry, then the node.</summary>
    // fidelity: M14-011
    public int RemoveUser(EnrolledFaceEntry entry)
    {
        lock (_gate) { RemoveUserCore(entry); return 0; }
    }

    private void RemoveUserCore(EnrolledFaceEntry entry)
    {
        _trackToFace.Remove(entry.CurrentTrack);
        _trackToFace.Remove(entry.PreviousTrack);
        foreach (var albumEntry in entry.AlbumEntries.Keys)
        {
            _albumEntryToFaceId.Remove(albumEntry);
            if (_album is not null && _okao.ClearUser(_album, albumEntry) != 0)
                Log?.Invoke($"RemoveUser: OKAO_FR_ClearUser failed for album entry {albumEntry}");
        }
        _faces.Remove(entry.FaceID);
    }

    /// <summary>
    /// <c>MergeFaces(keepID, mergeID)</c> 0x008638AC (G2-5/C4-5): identical ids succeed at once; both
    /// entries must exist; <c>MergeWith</c> runs with limit 5; keep's retained album entries are remapped
    /// to keepID; each removed entry and each of merge's remaining entries still owned by mergeID is
    /// erased from the LUT and cleared in OKAO (failure returns 1); merge's album map is destroyed and its
    /// current entry set to -1; then the full <c>RemoveUser(mergeID)</c> runs.
    /// </summary>
    // fidelity: M14-011
    public int MergeFaces(int keepID, int mergeID)
    {
        lock (_gate)
        {
            if (keepID == mergeID) return 0;
            if (!_faces.TryGetValue(keepID, out var keep) || !_faces.TryGetValue(mergeID, out var merge)) return 1;
            var removed = new List<int>();
            MergeWith(keep, merge, MergeLimit, removed);
            foreach (var a in keep.AlbumEntries.Keys) _albumEntryToFaceId[a] = keepID;
            foreach (var a in removed)
            {
                _albumEntryToFaceId.Remove(a);
                if (_album is not null && _okao.ClearUser(_album, a) != 0) return 1;
            }
            foreach (var a in merge.AlbumEntries.Keys.ToList())
            {
                if (!_albumEntryToFaceId.TryGetValue(a, out int owner) || owner != mergeID) continue;
                _albumEntryToFaceId.Remove(a);
                if (_album is not null && _okao.ClearUser(_album, a) != 0) return 1;
            }
            merge.AlbumEntries.Clear();
            merge.CurrentAlbumEntryId = -1;
            RemoveUser(mergeID);
            return 0;
        }
    }

    /// <summary>
    /// <c>EnrolledFaceEntry::MergeWith(other, limit, removed)</c> 0x008616AE (C3-17/C3-18): the album-entry
    /// merge (one session-only entry, the newer kept; at most <c>limit-1</c> non-session entries by
    /// recency) and the scalar merge.  Removed entries that belonged to <c>this</c> are appended.
    /// </summary>
    // fidelity: M14-011
    public static void MergeWith(EnrolledFaceEntry self, EnrolledFaceEntry other, int limit, List<int> removed)
    {
        var mine = self.AlbumEntries.Values.ToList();
        var theirs = other.AlbumEntries.Values.ToList();

        EnrolledFaceAlbumEntry? mySession = mine.FirstOrDefault(a => a.SessionOnly);
        EnrolledFaceAlbumEntry? theirSession = theirs.FirstOrDefault(a => a.SessionOnly);
        EnrolledFaceAlbumEntry? keptSession;
        if (mySession is not null && theirSession is not null)
        {
            keptSession = mySession.LastSeenMicros >= theirSession.LastSeenMicros ? mySession : theirSession;
            if (keptSession == theirSession) removed.Add(mySession.AlbumEntryId);   // this lost the session race
        }
        else keptSession = mySession ?? theirSession;

        var nonSession = mine.Where(a => !a.SessionOnly).Concat(theirs.Where(a => !a.SessionOnly))
                             .OrderByDescending(a => a.LastSeenMicros).ToList();
        var keepNon = nonSession.Take(Math.Max(0, limit - 1)).ToList();
        var keepSet = new HashSet<int>(keepNon.Select(a => a.AlbumEntryId));
        if (keptSession is not null) keepSet.Add(keptSession.AlbumEntryId);

        foreach (var a in mine)
            if (!keepSet.Contains(a.AlbumEntryId) && !removed.Contains(a.AlbumEntryId)) removed.Add(a.AlbumEntryId);

        self.AlbumEntries.Clear();
        if (keptSession is not null) self.AlbumEntries[keptSession.AlbumEntryId] = keptSession;
        foreach (var a in keepNon) self.AlbumEntries[a.AlbumEntryId] = a;
        self.CurrentAlbumEntryId = keptSession?.AlbumEntryId ?? -1;

        // C3-18 scalar merge.
        if (other.Offset04 == other.FaceID) self.Offset04 = other.Offset04;
        if (self.Name.Length == 0) self.Name = other.Name;
        self.Timestamp30Micros = Math.Min(self.Timestamp30Micros, other.Timestamp30Micros);
        self.Timestamp28Micros = Math.Max(self.Timestamp28Micros, other.Timestamp28Micros);
        self.Score = Math.Max(self.Score, other.Score);
    }

    /// <summary>
    /// <c>ConvertToEnrolledFaceStorage</c> 0x00860D7C (C3-19): the stored object's two seconds
    /// timestamps, the current/session album entry first (timestamp zero when -1) and then every other
    /// entry from the map, the face id and the name.
    /// </summary>
    // fidelity: M14-011
    public static StoredEnrolledFace ConvertToEnrolledFaceStorage(EnrolledFaceEntry entry)
    {
        var stored = new StoredEnrolledFace
        {
            Timestamp28Seconds = entry.Timestamp28Micros / MicrosPerSecond,
            Timestamp30Seconds = entry.Timestamp30Micros / MicrosPerSecond,
            FaceID = entry.FaceID,
            Name = entry.Name,
        };
        if (entry.CurrentAlbumEntryId >= 0 && entry.AlbumEntries.TryGetValue(entry.CurrentAlbumEntryId, out var cur))
        {
            stored.AlbumEntryIds.Add(entry.CurrentAlbumEntryId);
            stored.AlbumEntrySeconds.Add(cur.LastSeenMicros / MicrosPerSecond);
        }
        else { stored.AlbumEntryIds.Add(-1); stored.AlbumEntrySeconds.Add(0); }
        foreach (var kv in entry.AlbumEntries)
        {
            if (kv.Key == entry.CurrentAlbumEntryId) continue;
            stored.AlbumEntryIds.Add(kv.Key);
            stored.AlbumEntrySeconds.Add(kv.Value.LastSeenMicros / MicrosPerSecond);
        }
        return stored;
    }

    /// <summary>
    /// The inverse <c>EnrolledFaceEntry(EnrolledFaceStorage)</c> 0x00860728 (C3-20): restores face
    /// id/name, score 1000, multiplies the seconds by 1,000,000, makes the first album id the current
    /// entry, inserts each non--1 id, warns on a length mismatch and clips future times to now.
    /// </summary>
    // fidelity: M14-011
    public static EnrolledFaceEntry FromStorage(StoredEnrolledFace stored, long nowMicros)
    {
        var entry = new EnrolledFaceEntry { FaceID = stored.FaceID, Name = stored.Name, Score = RegisteredUserScore };
        entry.Timestamp28Micros = stored.Timestamp28Seconds * MicrosPerSecond;
        entry.Timestamp30Micros = stored.Timestamp30Seconds * MicrosPerSecond;
        if (stored.AlbumEntryIds.Count != stored.AlbumEntrySeconds.Count)
        {
            // warned; no entries installed
            return entry;
        }
        for (int i = 0; i < stored.AlbumEntryIds.Count; i++)
        {
            long micros = stored.AlbumEntrySeconds[i] * MicrosPerSecond;
            if (micros > nowMicros) micros = nowMicros;
            if (i == 0) entry.CurrentAlbumEntryId = stored.AlbumEntryIds[i];
            if (stored.AlbumEntryIds[i] != -1)
                entry.AlbumEntries[stored.AlbumEntryIds[i]] = new EnrolledFaceAlbumEntry
                { AlbumEntryId = stored.AlbumEntryIds[i], LastSeenMicros = micros, SessionOnly = i == 0 };
        }
        return entry;
    }

    /// <summary>
    /// C3-21/C4-1: the enrollment-data container.  Bytes 0..3 are the <c>0x0002FACE</c> u32 LE prefix and
    /// 4..7 the int32 LE nextFaceID; then zero or more records concatenated with no count or prefix.
    /// Rejects an empty buffer and a buffer of 11 bytes or fewer; the loop runs while
    /// <c>offset &lt; size-3</c>; a malformed record aborts the whole load (null).
    /// </summary>
    // fidelity: M14-011
    public static List<EnrolledFaceEntry>? DeserializeEnrollment(byte[] data, long nowMicros, out int nextFaceId)
    {
        nextFaceId = 0;
        if (data is null || data.Length == 0) return null;
        if (data.Length <= 11) return null;
        if (BitConverter.ToUInt32(data, 0) != StoredEnrolledFace.VersionPrefix) return null;
        nextFaceId = BitConverter.ToInt32(data, 4);
        var entries = new List<EnrolledFaceEntry>();
        int pos = StoredEnrolledFace.HeaderBytes;
        while (pos < data.Length - 3)
        {
            if (pos == data.Length) return null;
            var s = StoredEnrolledFace.Unpack(data, ref pos);
            if (s is null) return null;
            entries.Add(FromStorage(s, nowMicros));
        }
        return entries;
    }

    /// <summary>
    /// Serializes the current enrolled faces into the enrollment-data container (C3-19/C3-21/C4-1/C5-9..C5-11):
    /// no enrolled faces -> an empty array (no header); otherwise the 8-byte header (0x0002FACE, then
    /// <c>_nextFaceId</c> int32 LE) and then only <b>named</b> entries in ascending face-id order.
    /// </summary>
    // fidelity: M14-011
    public byte[] SerializeEnrollment()
    {
        lock (_gate)
        {
            if (_faces.Count == 0) return Array.Empty<byte>();     // C5-9: no header
            var b = new List<byte>();
            b.AddRange(BitConverter.GetBytes(StoredEnrolledFace.VersionPrefix));
            b.AddRange(BitConverter.GetBytes(_nextFaceId));
            foreach (var e in _faces.Values.Where(e => e.Name.Length != 0).OrderBy(e => e.FaceID))
                ConvertToEnrolledFaceStorage(e).Pack(b);           // C5-11: named only, face-id order
            return b.ToArray();
        }
    }

    /// <summary>
    /// <c>FaceRecognizer::SetSerializedData</c> 0x00867F54 (C3-22..C3-24): album restore, enrollment
    /// parsing, the consistency checks, the capacity install, nextFaceID, the loaded-face report and the
    /// temporary-handle cleanup.  0 on success, 1 on any failure.
    /// </summary>
    // fidelity: M14-011
    public int SetSerializedData(byte[] albumData, byte[] enrollmentData)
    {
        lock (_gate)
        {
            if (_common is null || _album is null) return 1;
            if (albumData is null || albumData.Length == 0) return 1;

            // C3-22/C4-8 album restore: the handle is the return, the status is the 4th argument.
            var loaded = _okao.RestoreAlbum(_common, albumData, albumData.Length, out int restoreResult);
            if (loaded is null || restoreResult != 0) return 1;
            if (_okao.GetRegisteredUserNum(loaded, out _) != 0) { _okao.DeleteAlbumHandle(loaded); return 1; }

            // C3-21 enrollment parse.
            var parsed = DeserializeEnrollment(enrollmentData ?? Array.Empty<byte>(), Now, out int nextFaceId);
            if (parsed is null) { _okao.DeleteAlbumHandle(loaded); return 1; }

            // C3-22 consistency.
            if (!CheckConsistency(loaded, parsed)) { _okao.DeleteAlbumHandle(loaded); return 1; }

            // C3-23 capacity compare and install.
            if (InstallAlbum(loaded) != 0) { _okao.DeleteAlbumHandle(loaded); return 1; }

            // C3-24 nextFaceID, maps, loaded-face report.
            _nextFaceId = nextFaceId;
            _albumEntryToFaceId.Clear();
            _faces.Clear();
            _trackToFace.Clear();
            foreach (var e in parsed)
            {
                _faces[e.FaceID] = e;
                foreach (var a in e.AlbumEntries.Keys) _albumEntryToFaceId[a] = e.FaceID;
            }
            FacesLoaded?.Invoke(parsed.Select(e => (e.FaceID, e.Name)).ToList());
            return 0;
        }
    }

    // C4-7/N6 SanityCheckBookkeeping: count == LUT size; every LUT key registered; every owned entry in the LUT with its owner; each failure logs.
    private bool CheckConsistency(OkaoAlbumHandle loaded, List<EnrolledFaceEntry> parsed)
    {
        var lut = new Dictionary<int, int>();
        foreach (var e in parsed)
            foreach (var a in e.AlbumEntries.Keys) lut[a] = e.FaceID;

        if (_okao.GetRegisteredUserNum(loaded, out int userCount) != 0)
        {
            Log?.Invoke("SanityCheckBookkeeping: OKAO_FR_GetRegisteredUserNum failed");
            return false;
        }
        if (userCount != lut.Count)
        {
            Log?.Invoke($"SanityCheckBookkeeping: FaceLibNumEntries={userCount}, AlbumEntryToFaceIDSize={lut.Count}");
            return false;
        }
        foreach (var key in lut.Keys)
        {
            if (_okao.IsRegistered(loaded, key, 0, out int result) != 0)
            {
                Log?.Invoke($"SanityCheckBookkeeping: OKAO_FR_IsRegistered failed for album entry {key}");
                return false;
            }
            if (result == 0)
            {
                Log?.Invoke($"SanityCheckBookkeeping: album entry {key} is not registered");
                return false;
            }
        }
        foreach (var e in parsed)
            foreach (var a in e.AlbumEntries.Keys)
                if (!lut.TryGetValue(a, out int owner) || owner != e.FaceID)
                {
                    Log?.Invoke($"SanityCheckBookkeeping: album entry {a} does not map back to face {e.FaceID}");
                    return false;
                }
        return true;
    }

    // C3-23/C5-4/C5-5: reject when either loaded capacity is larger; swap when both are equal; otherwise copy every registered pair.
    private int InstallAlbum(OkaoAlbumHandle loaded)
    {
        if (_okao.GetAlbumMaxNum(_album!, out int currentAlbumMax, out int currentDataMax) != 0) return 1;
        if (_okao.GetAlbumMaxNum(loaded, out int loadedAlbumMax, out int loadedDataMax) != 0) return 1;
        if (loadedAlbumMax > currentAlbumMax || loadedDataMax > currentDataMax) return 1;
        if (loadedAlbumMax == currentAlbumMax && loadedDataMax == currentDataMax)
        {
            var old = _album;
            _album = loaded;
            if (old is not null) _okao.DeleteAlbumHandle(old);
            return 0;
        }
        if (_okao.ClearAlbum(_album!) != 0) return 1;
        for (int albumIndex = 0; albumIndex < loadedAlbumMax; albumIndex++)
            for (int dataIndex = 0; dataIndex < loadedDataMax; dataIndex++)
            {
                if (_okao.IsRegistered(loaded, albumIndex, dataIndex, out int registered) != 0) return 1;
                if (registered == 0) continue;
                if (_okao.GetFeatureFromAlbum(loaded, albumIndex, dataIndex, out var feature) != 0) return 1;
                if (_okao.RegisterData(_album!, feature, albumIndex, dataIndex) != 0) return 1;
            }
        return 0;
    }
}