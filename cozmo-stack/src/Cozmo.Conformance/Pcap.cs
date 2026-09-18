using System.Buffers.Binary;

namespace Cozmo.Conformance;

public sealed record CapturedDatagram(double TimestampSec, string Src, int SrcPort, string Dst, int DstPort, byte[] Payload)
{
    public bool FromRobot => SrcPort is 5551 or 5552;
}

/// <summary>Minimal libpcap (.pcap) reader: Ethernet or Linux-cooked/raw-IP link types, IPv4, UDP only.</summary>
public static class Pcap
{
    public static IEnumerable<CapturedDatagram> Read(string path)
    {
        var d = File.ReadAllBytes(path);
        if (d.Length < 24) throw new FormatException("not a pcap file");
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(d);
        bool le = magic is 0xa1b2c3d4 or 0xa1b23c4d; bool nano = magic is 0xa1b23c4d or 0x4d3cb2a1;
        if (!le && magic is not (0xd4c3b2a1 or 0x4d3cb2a1)) throw new FormatException("unknown pcap magic (pcapng is not supported; convert with editcap -F pcap)");
        uint R32(int o) => le ? BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o)) : BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o));
        uint linkType = R32(20);
        int o2 = 24;
        while (o2 + 16 <= d.Length)
        {
            uint ts = R32(o2), tfrac = R32(o2 + 4), incl = R32(o2 + 8); o2 += 16;
            if (o2 + incl > d.Length) break;
            var pkt = d.AsSpan(o2, (int)incl); o2 += (int)incl;
            double t = ts + (nano ? tfrac / 1e9 : tfrac / 1e6);
            int ipOff = linkType switch { 1 => 14, 113 => 16, 101 => 0, 276 => 20, _ => -1 };
            if (ipOff < 0 || pkt.Length < ipOff + 20) continue;
            if (linkType == 1 && BinaryPrimitives.ReadUInt16BigEndian(pkt[12..]) != 0x0800) continue;
            var ip = pkt[ipOff..];
            if (ip[0] >> 4 != 4 || ip[9] != 17) continue;
            int ihl = (ip[0] & 0xf) * 4;
            var udp = ip[ihl..];
            if (udp.Length < 8) continue;
            int sp = BinaryPrimitives.ReadUInt16BigEndian(udp), dp = BinaryPrimitives.ReadUInt16BigEndian(udp[2..]);
            int ulen = BinaryPrimitives.ReadUInt16BigEndian(udp[4..]);
            var payload = udp.Slice(8, Math.Max(0, Math.Min(ulen - 8, udp.Length - 8))).ToArray();
            yield return new CapturedDatagram(t, $"{ip[12]}.{ip[13]}.{ip[14]}.{ip[15]}", sp, $"{ip[16]}.{ip[17]}.{ip[18]}.{ip[19]}", dp, payload);
        }
    }
}
