using System.Text;

namespace Cozmo.Robot;

// fidelity: M3-058
/// <summary>The one persisted DAS device id: read if present, otherwise created once. Hardware hosts pass the same id to Lab (M15-053).</summary>
public static class DasDeviceIdFile
{
    public static string DefaultPath=>Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cozmo-stack"), "device-id");
    public static string LoadOrCreate(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var reader = new StreamReader(path, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false);
                if (reader.ReadLine() is { } stored) return stored; // empty line is an ID; zero bytes is null.
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        string id = Guid.NewGuid().ToString(); // Java UUID.randomUUID().toString(), lowercase v4.
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(file, new UTF8Encoding(false)) { NewLine = ((char)10).ToString() };
            writer.WriteLine(id);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return id; // DAS.java returns the new ID even when mkdir/create/write fails.
    }
}
