using System.Security.Cryptography;

namespace Cozmo.Robot.Emulation;

/// <summary>
/// The shipped armeabi-v7a libraries the emulator may load: the load base of each (<c>emu_elf.BASES</c>, plus
/// libacattsandroid.so at 0x30000000 as the Acapela probe placed it), the symbol-resolution order (<c>emu_elf.ORDER</c> with
/// libacattsandroid appended), and the SHA-256 of the build each is pinned to. A file with any other hash is refused.
/// The libraries are the user's own copy from the APK (<c>resources/lib/armeabi-v7a/</c>); they are never committed.
/// </summary>
public static class ShippedLibraries
{
    public sealed record Pin(string Name, uint Base, string Sha256);

    /// <summary>In resolution order.</summary>
    public static readonly IReadOnlyList<Pin> All = new[]
    {
        new Pin("libc++_shared.so", 0x10000000, "8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a"),
        new Pin("libopencv_core.so", 0x14000000, "8b8c9fcdff9028e5c1b58578d6c20040bebfc2f16c2d2d4bf885f79da56cba8d"),
        new Pin("libopencv_imgproc.so", 0x18000000, "3c4e3ff7e639c61e21c5ef91ea2cc4125bb9bd259ff53f5c0b1eeb9a95054830"),
        new Pin("libopencv_imgcodecs.so", 0x1C000000, "4bdf5a45023c7938924f6c0018bf20774ebb5f7c0b2a637c93fe9e61b1f87218"),
        new Pin("libcozmoEngine.so", 0x20000000, "02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1"),
        new Pin("libacattsandroid.so", 0x30000000, "73c4db9455e1cc0235f731ab110194dbfb7aa44974af7ac74c11de382f135698"),
    };

    public static Pin Get(string name) =>
        All.FirstOrDefault(p => p.Name == name) ?? throw new ArgumentException($"{name} is not a pinned shipped library");

    /// <summary>Reads a library and checks its pin; a mismatch is an error naming both hashes.</summary>
    public static byte[] ReadPinned(string libDir, string name)
    {
        var pin = Get(name);
        var path = Path.Combine(libDir, name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{name} not found in {libDir} (the APK's lib/armeabi-v7a, extracted locally)", path);
        var raw = File.ReadAllBytes(path);
        var got = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
        if (got != pin.Sha256)
            throw new InvalidDataException($"{path} is not the pinned build: sha256 {got}, expected {pin.Sha256}");
        return raw;
    }

    /// <summary>resources/lib/armeabi-v7a in the nearest directory above <paramref name="start"/> that has it, or null.</summary>
    public static string? FindLibDir(string? start = null)
    {
        for (var d = new DirectoryInfo(start ?? AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "resources", "lib", "armeabi-v7a");
            if (File.Exists(Path.Combine(p, "libc++_shared.so"))) return p;
        }
        return null;
    }
}
