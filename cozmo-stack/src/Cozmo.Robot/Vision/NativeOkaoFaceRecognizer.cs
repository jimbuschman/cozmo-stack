using Cozmo.Robot.Emulation;

namespace Cozmo.Robot.Vision;

// Stock-only adapter. Every handle belongs to the shared DT/PT machine. UseSession serializes
// native calls with video detection and its recognition worker.
internal sealed class NativeOkaoFaceRecognizer : IOkaoFaceRecognizer
{
    private readonly OkaoFaceDetector _detector;
    internal NativeOkaoFaceRecognizer(OkaoFaceDetector detector) => _detector = detector;
    internal OkaoFaceDetector Detector => _detector;
    public bool IsAvailable => _detector.IsAvailable;
    public string Description => "Shipped OKAO face recognition on the detector's ARM machine";

    private T Run<T>(Func<OkaoFaceDetection, T> operation) => _detector.UseSession(operation);
    public OkaoCommonHandle CreateCommon() => Run(m => new OkaoCommonHandle(m, m.FrCommon));
    public OkaoAlbumHandle CreateAlbum() => Run(m => new OkaoAlbumHandle(m, m.FrCreateAlbum()));

    private static uint Pointer(OkaoFaceDetection machine, object? owner, uint pointer)
        => ReferenceEquals(machine, owner) && pointer != 0
            ? pointer : throw new InvalidOperationException("OKAO recognition handle belongs to another detector");
    private static uint Album(OkaoFaceDetection m, OkaoAlbumHandle album) => Pointer(m, album.Owner, album.Handle);
    private static uint Feature(OkaoFaceDetection m, OkaoFaceFeature feature) => Pointer(m, feature.Owner, feature.Handle);

    public int GetRegisteredUserNum(OkaoAlbumHandle album, out int count)
    {
        var result = Run(m => m.FrOneOut("OKAO_FR_GetRegisteredUserNum", new[] { Album(m, album) }));
        count = result.Value;
        return result.Result;
    }

    public int Identify(OkaoFaceFeature feature, OkaoAlbumHandle album, int maxResults, int[] ids, int[] scores, out int count)
    {
        var result = Run(m => m.FrIdentify(Feature(m, feature), Album(m, album), maxResults, ids, scores));
        count = result.Count;
        return result.Result;
    }

    public int RegisterData(OkaoAlbumHandle album, OkaoFaceFeature feature, int entry, int index)
        => Run(m => m.FrCall("OKAO_FR_RegisterData", Album(m, album), Feature(m, feature), unchecked((uint)entry), unchecked((uint)index)));

    public int GetRegisteredUsrDataNum(OkaoAlbumHandle album, int entry, out int count)
    {
        var result = Run(m => m.FrOneOut("OKAO_FR_GetRegisteredUsrDataNum", new[] { Album(m, album), unchecked((uint)entry) }));
        count = result.Value;
        return result.Result;
    }

    public int ClearUser(OkaoAlbumHandle album, int entry)
        => Run(m => m.FrCall("OKAO_FR_ClearUser", Album(m, album), unchecked((uint)entry)));
    public int ClearData(OkaoAlbumHandle album, int entry, int index)
        => Run(m => m.FrCall("OKAO_FR_ClearData", Album(m, album), unchecked((uint)entry), unchecked((uint)index)));

    public int Verify(OkaoFaceFeature feature, OkaoAlbumHandle album, int entry, out int score)
    {
        var result = Run(m => m.FrOneOut("OKAO_FR_Verify", new[] { Feature(m, feature), Album(m, album), unchecked((uint)entry) }));
        score = result.Value;
        return result.Result;
    }

    public int GetFeatureFromAlbum(OkaoAlbumHandle album, int entry, int index, out OkaoFaceFeature feature)
    {
        var result = Run(m => m.FrGetFeature(Album(m, album), entry, index));
        feature = result.Feature;
        return result.Result;
    }

    public int IsRegistered(OkaoAlbumHandle album, int entry, int index, out int registered)
    {
        var result = Run(m => m.FrOneOut("OKAO_FR_IsRegistered", new[] { Album(m, album), unchecked((uint)entry), unchecked((uint)index) }));
        registered = result.Value;
        return result.Result;
    }

    public OkaoAlbumHandle? RestoreAlbum(OkaoCommonHandle common, byte[] data, int size, out int status)
    {
        var result = Run(m => m.FrRestore(Pointer(m, common.Owner, common.Handle), data, size));
        status = result.Result;
        return result.Handle == 0 ? null : new OkaoAlbumHandle(result.Owner, result.Handle);
    }

    public int GetAlbumMaxNum(OkaoAlbumHandle album, out int albumMax, out int dataMax)
    {
        var result = Run(m => m.FrTwoOut("OKAO_FR_GetAlbumMaxNum", Album(m, album)));
        albumMax = result.First;
        dataMax = result.Second;
        return result.Result;
    }

    public int ClearAlbum(OkaoAlbumHandle album) => Run(m => m.FrCall("OKAO_FR_ClearAlbum", Album(m, album)));
    public int DeleteAlbumHandle(OkaoAlbumHandle album) => Run(m => m.FrCall("OKAO_FR_DeleteAlbumHandle", Album(m, album)));
    internal (int Status, byte[] Data) SerializeAlbum(OkaoAlbumHandle album) => Run(m => m.FrSerializeAlbum(Album(m, album)));
}
