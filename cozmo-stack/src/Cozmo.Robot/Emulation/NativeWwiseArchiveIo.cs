using System.Text;
using Cozmo.Robot.Animation.Wwise;

namespace Cozmo.Robot.Emulation;

// The native blocking device calls these resolver and low-level I/O vtables.  The
// device and its stream cache remain native; only the local file operations cross
// into managed code (0x8D6CC6, 0x8D6DBC, 0x8D6E88, 0x8D6F46).
internal sealed class NativeWwiseArchiveIo : IDisposable
{
    private readonly ArmMachine _machine;
    private readonly WwiseAnkiResolver _resolver;
    private readonly Dictionary<uint, WwiseFileDesc> _handles = new();
    private uint _nextHandle;
    internal uint Resolver { get; }
    internal uint Hook { get; }
    internal int Operations { get; private set; }

    internal NativeWwiseArchiveIo(ArmMachine machine, string archivePath)
    {
        _machine = machine;
        var manager = new WwiseStreamManager(new WwiseSteppedStreamIo(0));
        manager.Language.Set("English(US)");
        _resolver = new WwiseAnkiResolver(manager, new WwiseDiskFileSystem());
        if (_resolver.RegisterZip(archivePath, true) != 1)
            throw new IOException("Could not register the local Wwise audio archive");
        try
        {
            Resolver = Object((8, "shared_open_name", OpenName), (12, "shared_open_id", OpenId));
            Hook = Object((8, "shared_close", Close), (12, "shared_block_size", _ => 1),
                (16, "shared_description", _ => 0), (20, "shared_defer", _ => 1), (24, "shared_read", Read));
        }
        catch { Dispose(); throw; }
    }

    internal void Bind(int deviceId) => _resolver.BindNativeDevice(deviceId, true);
    internal int OpenHandles => _handles.Count;

    private uint Object(params (uint Offset, string Name, ImportHandler Callback)[] slots)
    {
        uint table = _machine.Malloc(0x40), obj = _machine.Malloc(4);
        _machine.Span(table, 0x40).Clear();
        foreach (var slot in slots)
        {
            _machine.Libc[slot.Name] = slot.Callback;
            _machine.W32(table + slot.Offset, _machine.Stub(slot.Name));
        }
        _machine.W32(obj, table);
        return obj;
    }

    private StubResult OpenName(ArmMachine m) => Open(m, Encoding.UTF8.GetString(m.CStr(m.Reg(1))), 0);
    private StubResult OpenId(ArmMachine m) => Open(m, null, m.Reg(1));

    private StubResult Open(ArmMachine m, string? name, uint id)
    {
        uint syncPtr = m.R32(m.Sp), descriptor = m.R32(m.Sp + 4), flagsPtr = m.Reg(3);
        bool sync = m.R8(syncPtr) != 0;
        WwiseFileFlags? flags = flagsPtr == 0 ? null : new WwiseFileFlags
        {
            CompanyId = m.R32(flagsPtr), CodecId = m.R32(flagsPtr + 4),
            LanguageSpecific = m.R8(flagsPtr + 0x10)
        };
        var fd = new WwiseFileDesc();
        int result = name is null ? _resolver.OpenById(id, m.Reg(2), flags, ref sync, fd)
            : _resolver.OpenByName(name, m.Reg(2), flags, ref sync, fd);
        m.Span(descriptor, 0x20).Clear();
        m.W8(syncPtr, sync ? (byte)1 : (byte)0);
        ulong size = unchecked((ulong)fd.FileSize);
        m.W32(descriptor, (uint)size); m.W32(descriptor + 4, (uint)(size >> 32));
        m.W32(descriptor + 8, fd.Word08); m.W32(descriptor + 12, fd.Word0C);
        m.W32(descriptor + 16, fd.Tag); m.W32(descriptor + 24, unchecked((uint)fd.DeviceId));
        if (fd.Handle is not null)
        {
            uint token = checked(++_nextHandle);
            _handles.Add(token, fd);
            m.W32(descriptor + 20, token);
        }
        Operations++;
        return unchecked((uint)result);
    }

    private StubResult Read(ArmMachine m)
    {
        uint descriptor = m.Reg(1), destination = m.Reg(3), transfer = m.R32(m.Sp);
        if (!_handles.TryGetValue(m.R32(descriptor + 20), out var fd)) throw new EmulationAbort("unknown archive read handle");
        uint capacity = m.R32(transfer + 8), count = m.R32(transfer + 12);
        if (count > capacity || count > 16 * 1024 * 1024) throw new EmulationAbort("invalid archive read size");
        var info = new WwiseTransferInfo
        {
            FilePosition = m.R32(transfer) | ((ulong)m.R32(transfer + 4) << 32),
            BufferSize = capacity, RequestedSize = count, Buffer = new byte[checked((int)count)]
        };
        int result = _resolver.Read(fd, info);
        if (result == 1) m.Write(destination, info.Buffer);
        Operations++;
        return unchecked((uint)result);
    }

    private StubResult Close(ArmMachine m)
    {
        uint descriptor = m.Reg(1), token = m.R32(descriptor + 20);
        if (!_handles.Remove(token, out var fd)) throw new EmulationAbort("unknown archive close handle");
        int result = _resolver.Close(fd);
        m.W32(descriptor + 20, 0);
        Operations++;
        return unchecked((uint)result);
    }

    public void Dispose()
    {
        foreach (var fd in _handles.Values) _resolver.Close(fd);
        _handles.Clear();
        foreach (var archive in _resolver.Zips) archive.Index.Close();
    }
}
