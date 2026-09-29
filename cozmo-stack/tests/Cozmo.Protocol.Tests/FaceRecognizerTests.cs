using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M14-011 (<c>Anki::Vision::FaceRecognizer</c>): the Anki-side recognition, enrollment, merge, album
/// and serialization state machine over the OKAO_FR_* seam, as Correction C3 records it.  The expected
/// values come from the C3 rows and their instruction citations, never from the code under test.
/// </summary>
public class FaceRecognizerTests
{
    // ------------------------------------------------------------------ constants (C3 rows)

    /// <summary>
    /// The exact gates the rows name: the confident gate is strictly over 750 (0x2EF), the new-user
    /// ceiling is 550 (0x226), the lower-ranked named threshold is over 675 (0x2A4), the enrollment gate
    /// is 999,999 microseconds (0x000F423F), the four-entry data limit is 4 and the merge limit is 5.
    /// </summary>
    [Fact]
    public void M14_011_TheRecognitionConstantsAreTheEnginesOwn()
    {
        Assert.Equal(750, FaceRecognizer.ConfidentScore);
        Assert.Equal(550, FaceRecognizer.NewUserScoreCeiling);
        Assert.Equal(675, FaceRecognizer.LowerRankedThreshold);
        Assert.Equal(4, FaceRecognizer.MaxDataEntries);
        Assert.Equal(5, FaceRecognizer.MergeLimit);
        Assert.Equal(999_999L, FaceRecognizer.EnrollmentGateMicros);
        Assert.Equal(1000, FaceRecognizer.RegisteredUserScore);
        Assert.Equal(10, FaceRecognizer.MaxResults);
        Assert.Equal(1000, FaceRecognizer.MaxAlbumEntries);
        Assert.Equal(0x0002FACEu, StoredEnrolledFace.VersionPrefix);
        Assert.Equal(8, StoredEnrolledFace.HeaderBytes);
    }

    // ------------------------------------------------------------------ RecognizeFace (C3-1..C3-8)

    /// <summary>C3-3: the confident match requires a top score strictly greater than 750 (0x2EF).</summary>
    [Fact]
    public void M14_011_RecognizeFaceConfidentGateIsStrictlyOver750()
    {
        var okao = new FakeOkaoRecognizer();
        var rec = new FaceRecognizer(okao);
        rec.SetAllowedEnrollments(1, 0);
        Assert.Equal(0, rec.RegisterNewUser(new OkaoFaceFeature(), out int faceId));
        Assert.Equal(1, faceId);

        okao.IdentifyResults.Add((0, 750));                       // not > 750: below the gate
        Assert.Equal(0, rec.RecognizeFace(new OkaoFaceFeature(), out int id, out int score));
        Assert.Equal(0, id);
        Assert.Equal(0, score);

        okao.IdentifyResults.Clear();
        okao.IdentifyResults.Add((0, 751));                       // > 750: the confident match
        Assert.Equal(0, rec.RecognizeFace(new OkaoFaceFeature(), out id, out score));
        Assert.Equal(1, id);
        Assert.Equal(751, score);
    }

    /// <summary>
    /// C3-1/C3-7: an empty album with an active enrollment registers the first user (score 1000); below
    /// the confident gate a new user is added only when there are no results or the top is below 550.
    /// </summary>
    [Fact]
    public void M14_011_RecognizeFaceRegistersANewUserOnlyBelow550()
    {
        var okao = new FakeOkaoRecognizer();
        var rec = new FaceRecognizer(okao);

        // C3-1: empty album, enrollment active, no enrollment track -> first user, score 1000.
        rec.SetAllowedEnrollments(1, 0);
        rec.RegistrationAllowed = true;
        Assert.Equal(0, rec.RecognizeFace(new OkaoFaceFeature(), out int id, out int score));
        Assert.Equal(1, id);
        Assert.Equal(1000, score);
        Assert.Single(okao.Production.Entries);

        // C3-7: 549 -> new user, score 1000.
        rec.SetAllowedEnrollments(1, 0);
        okao.IdentifyResults.Clear();
        okao.IdentifyResults.Add((99, 549));
        Assert.Equal(0, rec.RecognizeFace(new OkaoFaceFeature(), out id, out score));
        Assert.Equal(1000, score);
        Assert.NotEqual(0, id);

        // C3-7: 550 is not below 550 -> success without a face.
        rec.SetAllowedEnrollments(1, 0);
        okao.IdentifyResults.Clear();
        okao.IdentifyResults.Add((99, 550));
        Assert.Equal(0, rec.RecognizeFace(new OkaoFaceFeature(), out id, out score));
        Assert.Equal(0, id);
        Assert.Equal(0, score);
    }

    /// <summary>
    /// C3-5: a session-only (empty-name) top match with a later named candidate over 675 selects the
    /// named candidate and merges the session id into it.  The 675 threshold is strict.
    /// </summary>
    [Fact]
    public void M14_011_RecognizeFaceLowerRankedNamedCandidateMustExceed675()
    {
        var okao = new FakeOkaoRecognizer();
        long now = 1_000_000_000;
        var rec = new FaceRecognizer(okao, () => now);

        rec.SetAllowedEnrollments(1, 0);
        Assert.Equal(0, rec.RegisterNewUser(new OkaoFaceFeature(), out int sessionId));   // album 0
        now += 2_000_000;
        rec.SetAllowedEnrollments(1, 0);
        Assert.Equal(0, rec.RegisterNewUser(new OkaoFaceFeature(), out int namedId));     // album 1
        var named = rec.EnrolledFaces.Single(e => e.FaceID == namedId);
        named.Name = "Jim";

        // 675 is not over 675: the session-only top is returned.
        okao.IdentifyResults.Clear();
        okao.IdentifyResults.Add((0, 900));
        okao.IdentifyResults.Add((1, 675));
        Assert.Equal(0, rec.RecognizeFace(new OkaoFaceFeature(), out int id, out int score));
        Assert.Equal(sessionId, id);
        Assert.Equal(900, score);
        Assert.Contains(rec.EnrolledFaces, e => e.FaceID == sessionId);

        // 676 is over 675: the named candidate is selected and the session id merged into it.
        okao.IdentifyResults.Clear();
        okao.IdentifyResults.Add((0, 900));
        okao.IdentifyResults.Add((1, 676));
        Assert.Equal(0, rec.RecognizeFace(new OkaoFaceFeature(), out id, out score));
        Assert.Equal(namedId, id);
        Assert.Equal(676, score);
        Assert.DoesNotContain(rec.EnrolledFaces, e => e.FaceID == sessionId);
        // C4-5: MergeFaces cleared the merged user's album entry (0) and the named entry (1) remains.
        Assert.DoesNotContain(0, okao.Production.Entries.Keys);
        Assert.Contains(1, okao.Production.Entries.Keys);
    }

    /// <summary>
    /// C3-8: an active named target below the confident gate searches the result list, outputs the
    /// target's score (or zero), updates its album entry and forces the output id to the target.
    /// </summary>
    [Fact]
    public void M14_011_RecognizeFaceActiveTargetForcesTheOutputID()
    {
        var okao = new FakeOkaoRecognizer();
        long now = 1_000_000_000;
        var rec = new FaceRecognizer(okao, () => now);
        rec.CurrentTrack = 5;
        rec.RegistrationAllowed = true;                            // C4-3: the low-confidence gate needs +0xFC != 0

        // A named enrolled face on album entry 3.
        var f0 = new OkaoFaceFeature();
        rec.SetAllowedEnrollments(1, 0);
        Assert.Equal(0, rec.RegisterNewUser(f0, out int faceId));
        var entry = rec.EnrolledFaces.Single(e => e.FaceID == faceId);
        entry.Name = "Jim";
        int album = entry.CurrentAlbumEntryId;
        okao.IdentifyResults.Add((album, 700));                   // below the confident gate

        rec.SetAllowedEnrollments(1, faceId);                     // +0x100 = faceId, +0x104 = entry track
        Assert.Equal(faceId, rec.EnrollmentTargetId);
        Assert.Equal(0, rec.RecognizeFace(new OkaoFaceFeature(), out int id, out int score));
        Assert.Equal(faceId, id);
        Assert.Equal(700, score);

        // Not in the result list: the id is still forced, the score stays 0.
        okao.IdentifyResults.Clear();
        okao.IdentifyResults.Add((999, 700));
        rec.SetAllowedEnrollments(1, faceId);
        Assert.Equal(0, rec.RecognizeFace(new OkaoFaceFeature(), out id, out score));
        Assert.Equal(faceId, id);
        Assert.Equal(0, score);
    }

    // ------------------------------------------------------------------ UpdateExistingAlbumEntry (C3-12..C3-15)

    /// <summary>
    /// C3-14: with four data entries, replacement verifies the new feature against the full user and
    /// each held-out feature against the remaining three, then replaces the held-out index with the
    /// lowest score only when it is below the new feature's full-user score.
    /// </summary>
    [Fact]
    public void M14_011_UpdateExistingAlbumEntryReplacesTheLowestHeldOutFeature()
    {
        var okao = new FakeOkaoRecognizer();
        long now = 1_000_000_000;
        var rec = new FaceRecognizer(okao, () => now);
        rec.SetAllowedEnrollments(10, 0);

        var f0 = new OkaoFaceFeature();
        Assert.Equal(0, rec.RegisterNewUser(f0, out _));
        var f1 = new OkaoFaceFeature(); var f2 = new OkaoFaceFeature(); var f3 = new OkaoFaceFeature();
        now += 2_000_000; Assert.Equal(0, rec.UpdateExistingAlbumEntry(0, f1));
        now += 2_000_000; Assert.Equal(0, rec.UpdateExistingAlbumEntry(0, f2));
        now += 2_000_000; Assert.Equal(0, rec.UpdateExistingAlbumEntry(0, f3));
        Assert.Equal(4, okao.Production.Entries[0].Count);

        var f4 = new OkaoFaceFeature();
        okao.VerifyScores[f4] = 500;
        okao.VerifyScores[f0] = 100; okao.VerifyScores[f1] = 200; okao.VerifyScores[f2] = 300; okao.VerifyScores[f3] = 400;
        now += 2_000_000;
        Assert.Equal(0, rec.UpdateExistingAlbumEntry(0, f4));
        Assert.Same(f4, okao.Production.Entries[0][0]);           // index 0 held the lowest score
    }

    // ------------------------------------------------------------------ MergeWith (C3-17/C3-18)

    /// <summary>
    /// C3-17: with the caller's limit 5 the result is one session-only entry (the newer kept) plus at
    /// most four non-session entries by recency.  Entries dropped from <c>this</c> are appended to the
    /// removed vector; entries discarded only from <c>other</c> are not.
    /// </summary>
    [Fact]
    public void M14_011_MergeWithKeepsOneSessionEntryAndAtMostFourOthers()
    {
        var self = new EnrolledFaceEntry { FaceID = 1 };
        self.CurrentAlbumEntryId = 0;
        self.AlbumEntries[0] = new EnrolledFaceAlbumEntry { AlbumEntryId = 0, LastSeenMicros = 100, SessionOnly = true };
        self.AlbumEntries[1] = new EnrolledFaceAlbumEntry { AlbumEntryId = 1, LastSeenMicros = 10 };
        self.AlbumEntries[2] = new EnrolledFaceAlbumEntry { AlbumEntryId = 2, LastSeenMicros = 20 };
        self.AlbumEntries[3] = new EnrolledFaceAlbumEntry { AlbumEntryId = 3, LastSeenMicros = 30 };

        var other = new EnrolledFaceEntry { FaceID = 2 };
        other.CurrentAlbumEntryId = 10;
        other.AlbumEntries[10] = new EnrolledFaceAlbumEntry { AlbumEntryId = 10, LastSeenMicros = 200, SessionOnly = true };
        other.AlbumEntries[11] = new EnrolledFaceAlbumEntry { AlbumEntryId = 11, LastSeenMicros = 5 };
        other.AlbumEntries[12] = new EnrolledFaceAlbumEntry { AlbumEntryId = 12, LastSeenMicros = 6 };

        var removed = new List<int>();
        FaceRecognizer.MergeWith(self, other, FaceRecognizer.MergeLimit, removed);

        Assert.Equal(10, self.CurrentAlbumEntryId);               // other's session entry is newer
        Assert.Equal(5, self.AlbumEntries.Count);                 // one session + four others
        Assert.Equal(new[] { 1, 2, 3, 10, 12 }, self.AlbumEntries.Keys.OrderBy(k => k));
        Assert.True(self.AlbumEntries[10].SessionOnly);
        Assert.False(self.AlbumEntries[12].SessionOnly);
        Assert.Contains(0, removed);                              // this's losing session entry
        Assert.DoesNotContain(11, removed);                       // other's discarded entry is not cleared from this
        Assert.DoesNotContain(12, removed);
    }

    /// <summary>C3-18: the scalar merge copies +0x04 only when it equals the other face id, fills an empty name, and takes min/max.</summary>
    [Fact]
    public void M14_011_MergeWithScalarRules()
    {
        var self = new EnrolledFaceEntry { FaceID = 1, Name = "", Offset04 = 0, Score = 100, Timestamp28Micros = 50, Timestamp30Micros = 900 };
        var other = new EnrolledFaceEntry { FaceID = 7, Name = "Jim", Offset04 = 7, Score = 400, Timestamp28Micros = 200, Timestamp30Micros = 300 };
        FaceRecognizer.MergeWith(self, other, FaceRecognizer.MergeLimit, new List<int>());
        Assert.Equal(7, self.Offset04);                            // other.+4 == other.faceID, so copied
        Assert.Equal("Jim", self.Name);
        Assert.Equal(200, self.Timestamp28Micros);                 // later of +0x28
        Assert.Equal(300, self.Timestamp30Micros);                 // earlier of +0x30
        Assert.Equal(400, self.Score);

        var s2 = new EnrolledFaceEntry { FaceID = 1, Name = "Ann", Offset04 = 0 };
        var o2 = new EnrolledFaceEntry { FaceID = 7, Name = "Jim", Offset04 = 5 };
        FaceRecognizer.MergeWith(s2, o2, FaceRecognizer.MergeLimit, new List<int>());
        Assert.Equal(0, s2.Offset04);                              // other.+4 != other.faceID
        Assert.Equal("Ann", s2.Name);                              // name already set
    }

    // ------------------------------------------------------------------ SetAllowedEnrollments (C1-F11, M14-009)

    /// <summary>
    /// C1-F11: a nonzero id stores the mode at +0x108/+0x10c, the id at +0x100 and the enrolled entry's
    /// current track at +0x104 (0 with a warning when there is no record); a zero id cancels an in-flight
    /// enrollment (setting +0x5D when the target is nonzero and the count positive) and clears the target.
    /// </summary>
    [Fact]
    public void M14_011_SetAllowedEnrollmentsStoresAndCancelsTheTarget()
    {
        var okao = new FakeOkaoRecognizer();
        var rec = new FaceRecognizer(okao);
        rec.SetAllowedEnrollments(4, 7);
        Assert.Equal(4, rec.EnrollmentCount);
        Assert.Equal(4, rec.EnrollmentMode);
        Assert.Equal(7, rec.EnrollmentTargetId);
        Assert.Equal(0, rec.EnrollmentTrack);                      // no enrolled record for 7

        // A known face's current track becomes the enrollment track.
        long now = 1_000_000_000;
        var rec2 = new FaceRecognizer(okao, () => now);
        rec2.CurrentTrack = 3;
        rec2.SetAllowedEnrollments(1, 0);
        Assert.Equal(0, rec2.RegisterNewUser(new OkaoFaceFeature(), out int faceId));
        rec2.SetAllowedEnrollments(4, faceId);
        Assert.Equal(faceId, rec2.EnrollmentTargetId);
        Assert.Equal(3, rec2.EnrollmentTrack);                     // the entry's current track (+0x20)

        // Cancel: the target is cleared and the cancel flag is set.
        rec2.SetAllowedEnrollments(0, 0);
        Assert.Equal(0, rec2.EnrollmentTargetId);
        Assert.True(rec2.EnrollmentCancelled);
    }

    // ------------------------------------------------------------------ C4 corrections

    /// <summary>C4-2: GetNextFaceID starts at 1, wraps 0 to 1, and skips ids already in the enrolled-face map.</summary>
    [Fact]
    public void M14_011_GetNextFaceIDSkipsIdsAlreadyEnrolled()
    {
        var okao = new FakeOkaoRecognizer { ProductionCapacity = 1000, RestoredCapacity = 1000 };
        okao.RestoredEntries.Add(5);
        var rec = new FaceRecognizer(okao);
        var enrollment = EnrollmentFor((1, 5, "A"));
        BitConverter.GetBytes(1).CopyTo(enrollment, 4);            // nextFaceID = 1, and face 1 is enrolled
        Assert.Equal(0, rec.SetSerializedData(new byte[] { 1 }, enrollment));
        Assert.Equal(1, rec.NextFaceID);
        Assert.Equal(2, rec.GetNextFaceID());                      // 1 is present -> 2
    }

    /// <summary>
    /// C4-4: the ≥1 s enrollment gate reads the pre-update <c>EnrolledFaceEntry+0x30</c>, not the
    /// per-album-entry map node that <c>SetAlbumEntryLastSeenTime</c> writes.
    /// </summary>
    [Fact]
    public void M14_011_EnrollmentGateReadsThePreUpdateEntryTimestamp()
    {
        var okao = new FakeOkaoRecognizer();
        long now = 1_000_000_000;
        var rec = new FaceRecognizer(okao, () => now);
        rec.SetAllowedEnrollments(10, 0);
        Assert.Equal(0, rec.RegisterNewUser(new OkaoFaceFeature(), out int faceId));
        var entry = rec.EnrolledFaces.Single(e => e.FaceID == faceId);
        int album = entry.CurrentAlbumEntryId;

        // +0x30 is recent while the album map node is stale: the gate reads +0x30, so no data is added.
        entry.Timestamp30Micros = now;
        entry.AlbumEntries[album].LastSeenMicros = now - 2_000_000;
        Assert.Equal(0, rec.UpdateExistingAlbumEntry(album, new OkaoFaceFeature()));
        Assert.Equal(1, okao.Production.Entries[album].Count);

        // +0x30 is now stale: the gate opens and the data is added.
        entry.Timestamp30Micros = now - 2_000_000;
        Assert.Equal(0, rec.UpdateExistingAlbumEntry(album, new OkaoFaceFeature()));
        Assert.Equal(2, okao.Production.Entries[album].Count);
    }

    /// <summary>C4-6: the free-index probe consults OKAO only, not the Anki album-entry LUT.</summary>
    [Fact]
    public void M14_011_GetNextAlbumEntryToUseProbesOkaoOnly()
    {
        var okao = new FakeOkaoRecognizer();
        var rec = new FaceRecognizer(okao);
        // OKAO says album entry 0 is registered while the Anki LUT is empty.
        okao.Production.Entries[0] = new List<OkaoFaceFeature> { new OkaoFaceFeature() };
        Assert.Equal(1, rec.GetNextAlbumEntryToUse());
    }

    /// <summary>C4-6: when the album is full the oldest unnamed entry is removed and its album entry reused.</summary>
    [Fact]
    public void M14_011_GetNextAlbumEntryToUseRecyclesTheOldestUnnamedEntry()
    {
        var okao = new FakeOkaoRecognizer();
        long now = 1_000_000_000;
        var rec = new FaceRecognizer(okao, () => now);
        rec.SetAllowedEnrollments(10, 0);
        Assert.Equal(0, rec.RegisterNewUser(new OkaoFaceFeature(), out int older));
        now += 2_000_000;
        rec.SetAllowedEnrollments(10, 0);
        Assert.Equal(0, rec.RegisterNewUser(new OkaoFaceFeature(), out int newer));

        okao.RegisteredUserCountOverride = 1000;                   // the album is full
        Assert.Equal(0, rec.GetNextAlbumEntryToUse());             // the older entry's album entry
        Assert.DoesNotContain(rec.EnrolledFaces, e => e.FaceID == older);
        Assert.Contains(rec.EnrolledFaces, e => e.FaceID == newer);
    }

    // ------------------------------------------------------------------ serialization (C3-19..C3-21)

    /// <summary>
    /// C3-19/C3-20: ConvertToEnrolledFaceStorage emits the current/session entry first, then the rest;
    /// the inverse restores face id/name, score 1000, the microsecond timestamps, the current entry and
    /// each non--1 album id.
    /// </summary>
    [Fact]
    public void M14_011_EnrolledFaceStorageRoundTrips()
    {
        var e = new EnrolledFaceEntry
        {
            FaceID = 7, Name = "Jim", Offset04 = 7, Score = 400,
            CurrentTrack = 3, PreviousTrack = 2,
            Timestamp28Micros = 5_000_000, Timestamp30Micros = 9_000_000,
        };
        e.CurrentAlbumEntryId = 4;
        e.AlbumEntries[4] = new EnrolledFaceAlbumEntry { AlbumEntryId = 4, LastSeenMicros = 9_000_000, SessionOnly = true };
        e.AlbumEntries[5] = new EnrolledFaceAlbumEntry { AlbumEntryId = 5, LastSeenMicros = 8_000_000 };

        var stored = FaceRecognizer.ConvertToEnrolledFaceStorage(e);
        Assert.Equal(5, stored.Timestamp28Seconds);                // /1,000,000 (0x000F4240)
        Assert.Equal(9, stored.Timestamp30Seconds);
        Assert.Equal(new[] { 4, 5 }, stored.AlbumEntryIds);        // current first, then the rest
        Assert.Equal(new[] { 9L, 8L }, stored.AlbumEntrySeconds);
        Assert.Equal(7, stored.FaceID);
        Assert.Equal("Jim", stored.Name);

        var back = FaceRecognizer.FromStorage(stored, 20_000_000);
        Assert.Equal(7, back.FaceID);
        Assert.Equal("Jim", back.Name);
        Assert.Equal(1000, back.Score);                            // initialized to 1000
        Assert.Equal(5_000_000, back.Timestamp28Micros);
        Assert.Equal(9_000_000, back.Timestamp30Micros);
        Assert.Equal(4, back.CurrentAlbumEntryId);
        Assert.Equal(2, back.AlbumEntries.Count);
        Assert.True(back.AlbumEntries[4].SessionOnly);
        Assert.False(back.AlbumEntries[5].SessionOnly);

        // A vector-length mismatch installs no entries (C3-20).
        var mismatch = new StoredEnrolledFace { FaceID = 9 };
        mismatch.AlbumEntryIds.Add(1);
        var noEntries = FaceRecognizer.FromStorage(mismatch, 20_000_000);
        Assert.Empty(noEntries.AlbumEntries);

        // A restored future time is clipped to now (C3-20).
        var future = new StoredEnrolledFace { FaceID = 10 };
        future.AlbumEntryIds.Add(2); future.AlbumEntrySeconds.Add(100);
        var clipped = FaceRecognizer.FromStorage(future, 20_000_000);
        Assert.Equal(20_000_000, clipped.AlbumEntries[2].LastSeenMicros);
    }

    /// <summary>C3-19/C3-21/C4-1: the packed record round-trips and the size is exact (23 + 8*n1 + 4*n2 + len).</summary>
    [Fact]
    public void M14_011_EnrolledFaceStoragePackRoundTrips()
    {
        var stored = new StoredEnrolledFace { Timestamp28Seconds = 5, Timestamp30Seconds = 9, FaceID = 7, Name = "Jim" };
        stored.AlbumEntryIds.Add(4); stored.AlbumEntrySeconds.Add(9);
        stored.AlbumEntryIds.Add(5); stored.AlbumEntrySeconds.Add(8);
        var bytes = new List<byte>();
        stored.Pack(bytes);
        Assert.Equal(23 + 8 * 2 + 4 * 2 + 3, stored.Size());        // n1=2, n2=2, len=3 -> 50
        Assert.Equal(stored.Size(), bytes.Count);

        // The counts are one byte (C4-1): byte 16 is n1, and after n1 int64s comes the int32 face id.
        Assert.Equal(2, bytes[16]);
        Assert.Equal(7, BitConverter.ToInt32(bytes.ToArray(), 16 + 1 + 8 * 2));

        int pos = 0;
        var back = StoredEnrolledFace.Unpack(bytes.ToArray(), ref pos);
        Assert.NotNull(back);
        Assert.Equal(bytes.Count, pos);
        Assert.Equal(7, back!.FaceID);
        Assert.Equal("Jim", back.Name);
        Assert.Equal(new[] { 4, 5 }, back.AlbumEntryIds);
        Assert.Equal(new[] { 9L, 8L }, back.AlbumEntrySeconds);
    }

    /// <summary>
    /// C3-21/C4-1: the enrollment container rejects an empty buffer and one of 11 bytes or fewer,
    /// rejects a prefix other than 0x0002FACE, reads nextFaceID from bytes 4..7, and aborts on a
    /// malformed packed record.
    /// </summary>
    [Fact]
    public void M14_011_EnrollmentDataRejectsShortWrongPrefixAndMalformedRecords()
    {
        Assert.Null(FaceRecognizer.DeserializeEnrollment(Array.Empty<byte>(), 0, out _));
        Assert.Null(FaceRecognizer.DeserializeEnrollment(new byte[] { 1, 2, 3 }, 0, out _));

        var bad = new byte[12];
        BitConverter.GetBytes(0xDEADBEEFu).CopyTo(bad, 0);
        Assert.Null(FaceRecognizer.DeserializeEnrollment(bad, 0, out _));

        // A header-only container (8 bytes) is rejected by the size > 11 gate.
        var header = new List<byte>();
        header.AddRange(BitConverter.GetBytes(StoredEnrolledFace.VersionPrefix));
        header.AddRange(BitConverter.GetBytes(1234));
        Assert.Null(FaceRecognizer.DeserializeEnrollment(header.ToArray(), 0, out _));

        // A valid one-record container parses and carries nextFaceID.
        var good = EnrollmentFor((7, 5, "Jim"));
        BitConverter.GetBytes(1234).CopyTo(good, 4);
        var parsed = FaceRecognizer.DeserializeEnrollment(good, 0, out int nextFaceId);
        Assert.NotNull(parsed);
        Assert.Equal(7, Assert.Single(parsed!).FaceID);
        Assert.Equal(1234, nextFaceId);

        // A truncated record: the size check aborts the whole load.
        var truncated = new List<byte>(header);
        truncated.AddRange(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });
        Assert.Null(FaceRecognizer.DeserializeEnrollment(truncated.ToArray(), 0, out _));
    }

    // ------------------------------------------------------------------ album install (C3-22/C3-23)

    /// <summary>
    /// C3-23: equal loaded/current capacities take the swap path; a strictly smaller loaded album is
    /// copied entry-by-entry into the current album after ClearAlbum.
    /// </summary>
    [Fact]
    public void M14_011_AlbumInstallSwapsEqualCapacitiesAndCopiesSmaller()
    {
        // Equal capacities: the loaded handle is swapped into production.
        var equal = new FakeOkaoRecognizer { ProductionCapacity = 1000, RestoredCapacity = 1000 };
        equal.RestoredEntries.Add(5);
        var recEqual = new FaceRecognizer(equal);
        var production = recEqual.AlbumHandle;
        Assert.Equal(0, recEqual.SetSerializedData(new byte[] { 1 }, EnrollmentFor((1, 5, "A"))));
        Assert.NotSame(production, recEqual.AlbumHandle);
        Assert.Same(equal.LastRestored, recEqual.AlbumHandle);

        // Strictly smaller: the entries are copied into the existing production album.
        var smaller = new FakeOkaoRecognizer { ProductionCapacity = 1000, RestoredCapacity = 500 };
        smaller.RestoredEntries.AddRange(new[] { 5, 6 });
        var recSmaller = new FaceRecognizer(smaller);
        var productionSmaller = recSmaller.AlbumHandle;
        Assert.Equal(0, recSmaller.SetSerializedData(new byte[] { 1 }, EnrollmentFor((1, 5, "A"), (2, 6, "B"))));
        Assert.Same(productionSmaller, recSmaller.AlbumHandle);
        Assert.Equal(new[] { 5, 6 }, smaller.Production.Entries.Keys.OrderBy(k => k));
    }

    /// <summary>C3-23: a loaded album larger than the current capacity is rejected.</summary>
    [Fact]
    public void M14_011_AlbumInstallRejectsALargerLoadedAlbum()
    {
        var okao = new FakeOkaoRecognizer { ProductionCapacity = 500, RestoredCapacity = 1000 };
        okao.RestoredEntries.Add(5);
        var rec = new FaceRecognizer(okao);
        Assert.Equal(1, rec.SetSerializedData(new byte[] { 1 }, EnrollmentFor((1, 5, "A"))));
        Assert.Same(okao.ProductionAlbum, rec.AlbumHandle);
    }

    // ------------------------------------------------------------------ VisionSystem wiring

    /// <summary>
    /// The M14-011 wiring: <c>InstallSerializedFaceData</c> installs through the recognizer's
    /// <c>SetSerializedData</c> endpoint when the seam is available, and <c>GetSerializedFaceData</c>
    /// serializes the enrollment back out; <c>SetFaceEnrollmentMode</c> forwards Enroll's mode/id.
    /// </summary>
    [Fact]
    public void M14_011_VisionSystemWiresTheRecognizerAlbumAndEnrollmentPaths()
    {
        using var rig = new Rig();
        var okao = new FakeOkaoRecognizer { ProductionCapacity = 1000, RestoredCapacity = 1000 };
        okao.RestoredEntries.Add(5);
        using var vision = new VisionSystem(rig.Robot, rig.Cal, okaoRecognizer: okao);
        Assert.True(vision.Recognizer.IsAvailable);

        vision.InstallSerializedFaceData(new byte[] { 1 }, EnrollmentFor((1, 5, "A")));
        var loaded = Assert.Single(vision.Recognizer.EnrolledFaces);
        Assert.Equal(1, loaded.FaceID);
        Assert.Equal("A", loaded.Name);

        var (_, enrollment) = vision.GetSerializedFaceData();
        var back = FaceRecognizer.DeserializeEnrollment(enrollment, 0, out _);
        Assert.NotNull(back);
        Assert.Equal(1, Assert.Single(back!).FaceID);

        vision.SetFaceEnrollmentMode(1);
        Assert.Equal(1, vision.Recognizer.EnrollmentTargetId);
    }

    // ------------------------------------------------------------------ C5 corrections

    /// <summary>
    /// C5-1: the empty-album first-user branch tests the enrollment target id (+0x100), not the track
    /// (+0x104).  A nonzero target id skips the branch and runs the normal Identify path.
    /// </summary>
    [Fact]
    public void M14_011_EmptyAlbumFirstUserBranchTestsTheTargetID()
    {
        // target id 0, empty album -> first user.
        var okao = new FakeOkaoRecognizer();
        var rec = new FaceRecognizer(okao);
        rec.SetAllowedEnrollments(1, 0);
        Assert.Equal(0, rec.RecognizeFace(new OkaoFaceFeature(), out int id, out int score));
        Assert.Single(rec.EnrolledFaces);
        Assert.Equal(1000, score);

        // target id 5, empty album -> the branch is skipped and no user is registered.
        var okao2 = new FakeOkaoRecognizer();
        var rec2 = new FaceRecognizer(okao2);
        rec2.SetAllowedEnrollments(1, 5);
        Assert.Equal(5, rec2.EnrollmentTargetId);
        Assert.Equal(0, rec2.RecognizeFace(new OkaoFaceFeature(), out id, out score));
        Assert.Empty(rec2.EnrolledFaces);
        Assert.Equal(0, id);
    }

    /// <summary>
    /// C5-2: a later debug candidate with no enrolled record is inserted with an empty name; the top
    /// match's id is skipped; the list is capped at one extra.
    /// </summary>
    [Fact]
    public void M14_011_InstallDebugMatchesInsertsAMissingRecordWithAnEmptyName()
    {
        var okao = new FakeOkaoRecognizer();
        var rec = new FaceRecognizer(okao);
        rec.SetAllowedEnrollments(1, 0);
        Assert.Equal(0, rec.RegisterNewUser(new OkaoFaceFeature(), out int faceId));
        var entry = rec.EnrolledFaces.Single(e => e.FaceID == faceId);

        okao.IdentifyResults.Clear();
        okao.IdentifyResults.Add((0, 900));      // top, enrolled face
        okao.IdentifyResults.Add((0, 800));      // same face id -> skipped
        okao.IdentifyResults.Add((77, 790));     // no record -> inserted with an empty name
        okao.IdentifyResults.Add((78, 780));     // list already at two
        Assert.Equal(0, rec.RecognizeFace(new OkaoFaceFeature(), out _, out _));

        Assert.Equal(2, entry.DebugMatches.Count);
        Assert.Equal(faceId, entry.DebugMatches[0].FaceID);
        Assert.Equal(900, entry.DebugMatches[0].Score);
        Assert.Equal(0, entry.DebugMatches[1].FaceID);
        Assert.Equal(790, entry.DebugMatches[1].Score);
        Assert.Equal(string.Empty, entry.DebugMatches[1].Name);
    }

    /// <summary>
    /// C5-3: index 1 is the candidate; a different later record over the threshold falls back to the
    /// ordinary path; the same face id is skipped.
    /// </summary>
    [Fact]
    public void M14_011_LowerRankedLaterConflictFallsBackToTheOrdinaryPath()
    {
        var okao = new FakeOkaoRecognizer();
        long now = 1_000_000_000;
        var rec = new FaceRecognizer(okao, () => now);
        rec.SetAllowedEnrollments(1, 0);
        Assert.Equal(0, rec.RegisterNewUser(new OkaoFaceFeature(), out int sessionId));   // album 0
        now += 2_000_000;
        rec.SetAllowedEnrollments(1, 0);
        Assert.Equal(0, rec.RegisterNewUser(new OkaoFaceFeature(), out int namedId));     // album 1
        now += 2_000_000;
        rec.SetAllowedEnrollments(1, 0);
        Assert.Equal(0, rec.RegisterNewUser(new OkaoFaceFeature(), out int thirdId));     // album 2
        rec.EnrolledFaces.Single(e => e.FaceID == namedId).Name = "Jim";
        rec.EnrolledFaces.Single(e => e.FaceID == thirdId).Name = "Bob";

        // index 1 named (676); index 2 is a different found record over min(676-75,600)=600 -> ordinary.
        okao.IdentifyResults.Clear();
        okao.IdentifyResults.Add((0, 900));
        okao.IdentifyResults.Add((1, 676));
        okao.IdentifyResults.Add((2, 650));
        Assert.Equal(0, rec.RecognizeFace(new OkaoFaceFeature(), out int id, out int score));
        Assert.Equal(sessionId, id);
        Assert.Equal(900, score);

        // index 2 maps to the same face id -> skipped, index 1 is selected and merged.
        okao.IdentifyResults.Clear();
        okao.IdentifyResults.Add((0, 900));
        okao.IdentifyResults.Add((1, 676));
        okao.IdentifyResults.Add((1, 650));
        Assert.Equal(0, rec.RecognizeFace(new OkaoFaceFeature(), out id, out score));
        Assert.Equal(namedId, id);
        Assert.Equal(676, score);
    }

    /// <summary>C5-4: a loaded data capacity larger than the current one is rejected.</summary>
    [Fact]
    public void M14_011_AlbumInstallRejectsALargerLoadedDataCapacity()
    {
        var okao = new FakeOkaoRecognizer
        {
            ProductionCapacity = 1000, RestoredCapacity = 1000,
            ProductionDataCapacity = 4, RestoredDataCapacity = 8,
        };
        okao.RestoredEntries.Add(5);
        var rec = new FaceRecognizer(okao);
        Assert.Equal(1, rec.SetSerializedData(new byte[] { 1 }, EnrollmentFor((1, 5, "A"))));
        Assert.Same(okao.ProductionAlbum, rec.AlbumHandle);
    }

    /// <summary>C5-5: with smaller capacities every registered (album, data) pair is copied into the current album.</summary>
    [Fact]
    public void M14_011_AlbumInstallCopiesEveryRegisteredPair()
    {
        var okao = new FakeOkaoRecognizer { ProductionCapacity = 10, RestoredCapacity = 6 };
        okao.RestoredEntries.AddRange(new[] { 5, 3 });
        var rec = new FaceRecognizer(okao);
        var production = rec.AlbumHandle;
        Assert.Equal(0, rec.SetSerializedData(new byte[] { 1 }, EnrollmentFor((1, 5, "A"), (2, 3, "B"))));
        Assert.Same(production, rec.AlbumHandle);
        Assert.Equal(new[] { 3, 5 }, okao.Production.Entries.Keys.OrderBy(k => k));
    }

    /// <summary>C5-6: GetNextFaceID leaves +0xf4 at the returned id.</summary>
    [Fact]
    public void M14_011_GetNextFaceIDLeavesTheCounterAtTheReturnedID()
    {
        var okao = new FakeOkaoRecognizer();
        var rec = new FaceRecognizer(okao);
        Assert.Equal(1, rec.GetNextFaceID());                      // free: seed returned, not stored
        Assert.Equal(1, rec.NextFaceID);

        var okao2 = new FakeOkaoRecognizer { ProductionCapacity = 1000, RestoredCapacity = 1000 };
        okao2.RestoredEntries.Add(5);
        var rec2 = new FaceRecognizer(okao2);
        var enrollment = EnrollmentFor((1, 5, "A"));
        BitConverter.GetBytes(1).CopyTo(enrollment, 4);            // nextFaceID = 1, face 1 present
        Assert.Equal(0, rec2.SetSerializedData(new byte[] { 1 }, enrollment));
        Assert.Equal(2, rec2.GetNextFaceID());
        Assert.Equal(2, rec2.NextFaceID);
    }

    /// <summary>C5-7: GetNextAlbumEntryToUse leaves +0xf8 at the returned free index.</summary>
    [Fact]
    public void M14_011_GetNextAlbumEntryToUseLeavesTheProbeAtTheReturnedIndex()
    {
        var okao = new FakeOkaoRecognizer();
        var rec = new FaceRecognizer(okao);
        okao.Production.Entries[0] = new List<OkaoFaceFeature> { new OkaoFaceFeature() };
        okao.Production.Entries[1] = new List<OkaoFaceFeature> { new OkaoFaceFeature() };
        Assert.Equal(2, rec.GetNextAlbumEntryToUse());             // 0,1 registered; 2 free
        Assert.Equal(2, rec.GetNextAlbumEntryToUse());             // +0xf8 is still 2
    }

    /// <summary>C5-9/C5-10/C5-11: the writer emits no header with no faces, and only named entries in face-id order.</summary>
    [Fact]
    public void M14_011_SerializeEnrollmentWritesOnlyNamedEntriesInFaceIdOrder()
    {
        var okao = new FakeOkaoRecognizer { ProductionCapacity = 1000, RestoredCapacity = 1000 };
        okao.RestoredEntries.AddRange(new[] { 5, 6 });
        var rec = new FaceRecognizer(okao);
        Assert.Equal(0, rec.SetSerializedData(new byte[] { 1 }, EnrollmentFor((2, 6, "B"), (1, 5, "A"))));
        rec.SetAllowedEnrollments(1, 0);
        Assert.Equal(0, rec.RegisterNewUser(new OkaoFaceFeature(), out _));   // unnamed -> skipped

        var bytes = rec.SerializeEnrollment();
        var back = FaceRecognizer.DeserializeEnrollment(bytes, 0, out _);
        Assert.NotNull(back);
        Assert.Equal(new[] { 1, 2 }, back!.Select(e => e.FaceID).ToArray());

        // No faces at all -> an empty vector, no header.
        var empty = new FaceRecognizer(new FakeOkaoRecognizer());
        Assert.Empty(empty.SerializeEnrollment());
    }

    // ------------------------------------------------------------------ helpers

    private static byte[] EnrollmentFor(params (int FaceId, int AlbumEntry, string Name)[] entries)
    {
        var b = new List<byte>();
        b.AddRange(BitConverter.GetBytes(StoredEnrolledFace.VersionPrefix));
        b.AddRange(BitConverter.GetBytes(100));
        foreach (var e in entries)
        {
            var s = new StoredEnrolledFace { FaceID = e.FaceId, Name = e.Name };
            s.AlbumEntryIds.Add(e.AlbumEntry);
            s.AlbumEntrySeconds.Add(10);
            s.Pack(b);
        }
        return b.ToArray();
    }

    /// <summary>An in-memory OKAO recognizer: the seam the C3 rows sit on, scripted for the tests.</summary>
    private sealed class FakeOkaoRecognizer : IOkaoFaceRecognizer
    {
        public sealed class AlbumState
        {
            public int Capacity;
            public int DataCapacity = 4;
            public readonly Dictionary<int, List<OkaoFaceFeature>> Entries = new();
        }

        private readonly Dictionary<OkaoAlbumHandle, AlbumState> _states = new();

        public int ProductionCapacity = 1000;
        public int RestoredCapacity = 1000;
        public int ProductionDataCapacity = 4;
        public int RestoredDataCapacity = 4;
        public OkaoAlbumHandle ProductionAlbum = null!;
        public OkaoAlbumHandle? LastRestored;
        public AlbumState Production => _states[ProductionAlbum];
        public readonly List<int> RestoredEntries = new();

        public readonly List<(int AlbumEntry, int Score)> IdentifyResults = new();
        public readonly Dictionary<OkaoFaceFeature, int> VerifyScores = new();
        public int DefaultVerifyScore = 500;
        public int IdentifyError;
        public int RegisterError;
        public int VerifyError;
        public int ClearDataError;
        public int GetFeatureError;
        public int IsRegisteredError;
        /// <summary>When >= 0, <see cref="GetRegisteredUserNum"/> reports this instead of the entry count.</summary>
        public int RegisteredUserCountOverride = -1;

        public bool IsAvailable => true;
        public string Description => "fake OKAO recognizer for tests";

        public OkaoCommonHandle CreateCommon() => new();
        public OkaoAlbumHandle CreateAlbum()
        {
            var h = new OkaoAlbumHandle();
            _states[h] = new AlbumState { Capacity = ProductionCapacity, DataCapacity = ProductionDataCapacity };
            ProductionAlbum = h;
            return h;
        }

        public int GetRegisteredUserNum(OkaoAlbumHandle album, out int count)
        {
            count = RegisteredUserCountOverride >= 0
                ? RegisteredUserCountOverride
                : (_states.TryGetValue(album, out var s) ? s.Entries.Count : 0);
            return 0;
        }

        public int Identify(OkaoFaceFeature feature, OkaoAlbumHandle album, int maxResults, int[] ids, int[] scores, out int count)
        {
            if (IdentifyError != 0) { count = 0; return IdentifyError; }
            count = IdentifyResults.Count;
            for (int i = 0; i < IdentifyResults.Count && i < ids.Length; i++)
            { ids[i] = IdentifyResults[i].AlbumEntry; scores[i] = IdentifyResults[i].Score; }
            return 0;
        }

        public int RegisterData(OkaoAlbumHandle album, OkaoFaceFeature feature, int albumEntry, int dataIndex)
        {
            if (RegisterError != 0) return RegisterError;
            var s = _states[album];
            if (!s.Entries.TryGetValue(albumEntry, out var list)) { list = new List<OkaoFaceFeature>(); s.Entries[albumEntry] = list; }
            while (list.Count <= dataIndex) list.Add(null!);
            list[dataIndex] = feature;
            return 0;
        }

        public int Verify(OkaoFaceFeature feature, OkaoAlbumHandle album, int albumEntry, out int score)
        {
            if (VerifyError != 0) { score = 0; return VerifyError; }
            score = VerifyScores.TryGetValue(feature, out var v) ? v : DefaultVerifyScore;
            return 0;
        }

        public int GetRegisteredUsrDataNum(OkaoAlbumHandle album, int albumEntry, out int count)
        {
            count = _states[album].Entries.TryGetValue(albumEntry, out var list) ? list.Count : 0;
            return 0;
        }

        public int ClearUser(OkaoAlbumHandle album, int albumEntry) { _states[album].Entries.Remove(albumEntry); return 0; }

        public int ClearData(OkaoAlbumHandle album, int albumEntry, int dataIndex)
        {
            if (ClearDataError != 0) return ClearDataError;
            // Held-out modelling: the slot is emptied but stays in place, and RegisterData refills it.
            if (_states[album].Entries.TryGetValue(albumEntry, out var list) && dataIndex < list.Count) list[dataIndex] = null!;
            return 0;
        }

        public int GetFeatureFromAlbum(OkaoAlbumHandle album, int albumEntry, int dataIndex, out OkaoFaceFeature feature)
        {
            feature = null!;
            if (GetFeatureError != 0) return GetFeatureError;
            if (_states[album].Entries.TryGetValue(albumEntry, out var list) && dataIndex < list.Count) { feature = list[dataIndex]; return 0; }
            return 1;
        }

        public int IsRegistered(OkaoAlbumHandle album, int albumEntry, int dataIndex, out int result)
        {
            result = 0;
            if (IsRegisteredError != 0) return IsRegisteredError;
            result = _states[album].Entries.TryGetValue(albumEntry, out var list) && dataIndex < list.Count ? 1 : 0;
            return 0;
        }

        public OkaoAlbumHandle? RestoreAlbum(OkaoCommonHandle common, byte[] data, int size, out int result)
        {
            var h = new OkaoAlbumHandle();
            var s = new AlbumState { Capacity = RestoredCapacity, DataCapacity = RestoredDataCapacity };
            foreach (var e in RestoredEntries) s.Entries[e] = new List<OkaoFaceFeature> { new OkaoFaceFeature() };
            _states[h] = s;
            LastRestored = h;
            result = 0;
            return h;
        }

        public int GetAlbumMaxNum(OkaoAlbumHandle album, out int albumMax, out int dataMax) { albumMax = _states[album].Capacity; dataMax = _states[album].DataCapacity; return 0; }
        public int ClearAlbum(OkaoAlbumHandle album) { _states[album].Entries.Clear(); return 0; }
        public int DeleteAlbumHandle(OkaoAlbumHandle album) { _states.Remove(album); return 0; }
    }
}