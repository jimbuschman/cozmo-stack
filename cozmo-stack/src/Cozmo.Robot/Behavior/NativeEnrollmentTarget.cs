using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Behavior;

// BehaviorEnrollFace::UpdateFaceToEnroll and IsSeeingTooManyFaces,
// 0x5FDC84..0x5FDFDA and 0x5FF018..0x5FF11A.
internal sealed class NativeEnrollmentTarget
{
    internal int Selected { get; set; }
    internal uint LastObserved { get; private set; }
    internal int WrongId { get; private set; }
    internal string WrongName { get; private set; } = string.Empty;
    internal float TooManySince { get; private set; }

    internal void Reset(int selected)
    {
        Selected = selected;
        LastObserved = 0;
        WrongId = 0;
        WrongName = string.Empty;
        TooManySince = 0;
    }
    internal void ClearWrong() { WrongId = 0; WrongName = string.Empty; }

    internal void Update(ManipulationSystem system, EngineRobot owner, int saveId, uint imageTimestamp,
        uint maxFaces, float recentSeconds, float now, Func<bool> current)
    {
        uint window = checked((uint)(recentSeconds * 1000f));
        uint since = imageTimestamp > window ? imageTimestamp - window : 0;
        var faces = system.Vision.Faces;
        var recent = faces.GetNativeFaceIds(owner, since);
        if (!current()) return;
        if ((uint)recent.Count > maxFaces)
        {
            if (TooManySince == 0)
            {
                TooManySince = now;
                system.Vision.SetFaceEnrollmentMode(0);
            }
            return;
        }
        if (TooManySince > 0)
        {
            TooManySince = 0;
            if (Selected != 0) system.Vision.SetFaceEnrollmentMode(Selected);
            if (!current()) return;
        }
        var observed = faces.GetNativeFaceIds(owner, imageTimestamp);
        if (!current()) return;
        int incoming = Selected;
        if (incoming != 0 && observed.Contains(incoming))
        {
            var face = faces.GetNativeFace(owner, incoming);
            if (face is null) throw new NativeActionUnavailable("M1-034 M14-008 enrollment selected face disappeared at 0x5FDD38");
            if (!current()) return;
            Accept(face.Id, face.LastObservedTimestamp);
            return;
        }
        foreach (int id in observed)
        {
            if (!current()) return;
            if (id <= 0 || faces.GetNativeFace(owner, id) is not { } candidate) continue;
            WrongId = id;
            WrongName = candidate.Name ?? string.Empty;
            if (candidate.HasName && id != saveId) continue;
            if (incoming != 0)
            {
                if (faces.GetNativeFace(owner, Selected) is null) continue;
                if (!faces.NativeFacesSame(owner, id, Selected)) continue;
                if (!current()) return;
            }
            Accept(candidate.Id, candidate.LastObservedTimestamp);
        }
    }

    private void Accept(int id, uint timestamp)
    {
        Selected = id;
        LastObserved = timestamp;
        WrongId = 0;
        WrongName = string.Empty;
    }
}
