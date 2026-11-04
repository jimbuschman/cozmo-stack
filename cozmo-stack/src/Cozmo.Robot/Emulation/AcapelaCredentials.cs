namespace Cozmo.Robot.Emulation;

/// <summary>
/// The Acapela licence the engine hands to <c>setLicense(userid, password, license)</c>, decoded by the engine's own code run
/// under the emulator (the Acapela probe's <c>tts_license.get</c>): AcapelaTTS::GetUserid 0x0069F8DC, GetPassword 0x0069F8F8
/// and GetLicense 0x0069F914 in libcozmoEngine.so, each a Thumb function called with no setup beyond relocation. The licence
/// text is returned as the engine's std::string bytes and is never logged.
/// </summary>
public sealed record AcapelaCredentials(uint UserId, uint Password, byte[] License)
{
    public const uint GetUseridAddr = 0x0069F8DC;
    public const uint GetPasswordAddr = 0x0069F8F8;
    public const uint GetLicenseAddr = 0x0069F914;

    /// <summary>Runs the three getters in a machine holding libc++_shared.so and libcozmoEngine.so (both pinned).</summary>
    public static AcapelaCredentials FromEngine(string libDir)
    {
        using var m = new ArmMachine(libDir, new[] { "libc++_shared.so", "libcozmoEngine.so" });
        m.Relocate();
        uint b = m.Library("libcozmoEngine.so").Base;
        uint uid = m.Call(b + GetUseridAddr + 1);
        uint pw = m.Call(b + GetPasswordAddr + 1);
        uint s = m.Malloc(16);
        m.Fill(s, 16, 0);
        m.Call(b + GetLicenseAddr + 1, new[] { s });
        byte[] lic;
        // libc++ std::string: long form when bit 0 of the first byte is set (size at +4, data at +8), else short form
        // (size = first byte >> 1, data at +1)
        if ((m.R8(s) & 1) != 0) lic = m.Read(m.R32(s + 8), m.R32(s + 4));
        else lic = m.Read(s + 1, (uint)(m.R8(s) >> 1));
        return new AcapelaCredentials(uid, pw, lic);
    }

    public override string ToString() => $"AcapelaCredentials(userid 0x{UserId:X}, password 0x{Password:X}, licence {License.Length} bytes)";
}
