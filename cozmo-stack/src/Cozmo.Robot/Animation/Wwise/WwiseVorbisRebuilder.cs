using System.Buffers.Binary;

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// Turns a Wwise Vorbis stream back into a standard Ogg Vorbis one.
///
/// Wwise encodes Vorbis but ships it without the Ogg container and with the three header packets stripped
/// down: the identification and comment headers are gone entirely, the codebooks in the setup header are
/// replaced by indices into an external library, and several fields are packed into fewer bits than the
/// specification uses. Audio packets lose their Ogg framing and, in this build, their leading packet-type
/// and window bits as well. This class puts all of that back.
///
/// Ported from ww2ogg by Adam Gashlin (BSD-3-Clause; see third-party/ww2ogg/LICENSE for the notice, and
/// the README there for provenance). Only what these files need is ported: this build's 1987 Vorbis files
/// are all the same shape — a 0x2A-byte vorb block inside the fmt chunk, external codebooks, no granule in
/// the packet headers — so the header-triad, old-packet-header, inline-codebook and full-setup paths that
/// ww2ogg carries for other games are not reproduced. A file that is not that shape is refused rather than
/// guessed at.
/// </summary>
public static class WwiseVorbisRebuilder
{
    /// <summary>
    /// Rebuilds a complete Ogg Vorbis file from a parsed <c>.wem</c>. Throws
    /// <see cref="InvalidDataException"/> with a specific reason if anything does not add up.
    /// </summary>
    public static byte[] ToOgg(WwiseMedia media, WwiseCodebookLibrary codebooks)
    {
        if (media.Codec != WwiseCodec.Vorbis || media.Vorbis is not { } vorb)
            throw new InvalidDataException($"not a Wwise Vorbis file: format tag 0x{media.FormatTag:X4}");

        var data = media.Data;
        if (vorb.SetupPacketOffset >= (uint)data.Length || vorb.FirstAudioPacketOffset > (uint)data.Length)
            throw new InvalidDataException("the setup or first audio packet offset is past the end of the data");

        var os = new OggWriter();
        WriteIdentification(os, media, vorb);
        WriteComment(os);
        var modeBlockFlag = WriteSetup(os, media, vorb, codebooks);
        WriteAudio(os, media, vorb, modeBlockFlag);
        return os.ToArray();
    }

    // ------------------------------------------------------------------ header packets

    private static void WriteIdentification(OggWriter os, WwiseMedia m, WwiseVorbisHeader v)
    {
        PacketHeader(os, 1);
        os.Write(0, 32);                                   // vorbis version
        os.Write((uint)m.Channels, 8);
        os.Write((uint)m.SampleRate, 32);
        os.Write(0, 32);                                   // bitrate maximum
        os.Write((uint)(m.AvgBytesPerSecond * 8), 32);     // bitrate nominal
        os.Write(0, 32);                                   // bitrate minimum
        os.Write(v.BlockSize0Pow, 4);
        os.Write(v.BlockSize1Pow, 4);
        os.PutBit(true);                                   // framing
        os.FlushPage();
    }

    private static void WriteComment(OggWriter os)
    {
        PacketHeader(os, 3);
        // The vendor string is free-form. Naming what rebuilt the stream keeps a decoded file traceable.
        const string vendor = "rebuilt from Audiokinetic Wwise by cozmo-stack, after ww2ogg";
        os.Write((uint)vendor.Length, 32);
        foreach (char c in vendor) os.Write((byte)c, 8);
        os.Write(0, 32);                                   // no user comments
        os.PutBit(true);                                   // framing
        os.FlushPage();
    }

    /// <summary>Writes the packet type byte and the "vorbis" signature every header packet opens with.</summary>
    private static void PacketHeader(OggWriter os, uint type)
    {
        os.Write(type, 8);
        foreach (char c in "vorbis") os.Write((byte)c, 8);
    }

    // ------------------------------------------------------------------ the setup packet

    /// <summary>
    /// Rebuilds the setup header, which carries the codebooks, floors, residues, mappings and modes.
    ///
    /// Returns each mode's block flag, which the audio loop needs: Wwise strips the window bits off every
    /// packet, and they can only be put back by knowing whether the mode a packet names is a long window.
    /// </summary>
    private static bool[] WriteSetup(OggWriter os, WwiseMedia m, WwiseVorbisHeader v, WwiseCodebookLibrary cbl)
    {
        PacketHeader(os, 5);

        int setupLength = (int)(v.FirstAudioPacketOffset - v.SetupPacketOffset);
        if (setupLength <= 2) throw new InvalidDataException("the setup packet is empty");

        // Packet headers in this shape are two bytes of size and no granule.
        var span = m.Data.Span;
        int at = (int)v.SetupPacketOffset;
        int packetSize = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(at, 2));
        if (packetSize <= 0 || at + 2 + packetSize > m.Data.Length)
            throw new InvalidDataException($"the setup packet claims {packetSize} bytes, which does not fit");

        var ss = new BitReader(m.Data.Slice(at + 2, packetSize));

        uint codebookCount = ss.Read(8) + 1;
        os.Write(codebookCount - 1, 8);
        for (uint i = 0; i < codebookCount; i++)
        {
            uint id = ss.Read(10);
            if (id >= (uint)cbl.Count)
                throw new InvalidDataException($"codebook index {id} is outside the packed library of {cbl.Count}");
            cbl.Rebuild((int)id, os);
        }

        // Time domain transforms: a placeholder the format keeps but never uses.
        os.Write(0, 6);
        os.Write(0, 16);

        uint floorCount = ss.Read(6) + 1;
        os.Write(floorCount - 1, 6);
        for (uint i = 0; i < floorCount; i++) RebuildFloor(ss, os, codebookCount);

        uint residueCount = ss.Read(6) + 1;
        os.Write(residueCount - 1, 6);
        for (uint i = 0; i < residueCount; i++) RebuildResidue(ss, os, codebookCount);

        uint mappingCount = ss.Read(6) + 1;
        os.Write(mappingCount - 1, 6);
        for (uint i = 0; i < mappingCount; i++) RebuildMapping(ss, os, m.Channels, floorCount, residueCount);

        uint modeCount = ss.Read(6) + 1;
        os.Write(modeCount - 1, 6);
        var modeBlockFlag = new bool[modeCount];
        for (uint i = 0; i < modeCount; i++)
        {
            bool blockFlag = ss.ReadBit();
            os.PutBit(blockFlag);
            modeBlockFlag[i] = blockFlag;
            os.Write(0, 16);                               // window type, only 0 is legal
            os.Write(0, 16);                               // transform type, only 0 is legal
            uint mapping = ss.Read(8);
            os.Write(mapping, 8);
            if (mapping >= mappingCount) throw new InvalidDataException("mode names a mapping that does not exist");
        }
        os.PutBit(true);                                   // framing
        os.FlushPage();

        if ((ss.BitsRead + 7) / 8 != packetSize)
            throw new InvalidDataException($"setup packet: consumed {(ss.BitsRead + 7) / 8} bytes of {packetSize}");
        return modeBlockFlag;
    }

    private static void RebuildFloor(BitReader ss, OggWriter os, uint codebookCount)
    {
        os.Write(1, 16);                                   // floor type; Wwise only ever emits type 1

        uint partitions = ss.Read(5);
        os.Write(partitions, 5);
        var partitionClass = new uint[partitions];
        uint maxClass = 0;
        for (uint j = 0; j < partitions; j++)
        {
            partitionClass[j] = ss.Read(4);
            os.Write(partitionClass[j], 4);
            maxClass = Math.Max(maxClass, partitionClass[j]);
        }

        var classDimensions = new uint[maxClass + 1];
        for (uint j = 0; j <= maxClass; j++)
        {
            uint dimsLess1 = ss.Read(3);
            os.Write(dimsLess1, 3);
            classDimensions[j] = dimsLess1 + 1;

            uint subclasses = ss.Read(2);
            os.Write(subclasses, 2);
            if (subclasses != 0)
            {
                uint masterbook = ss.Read(8);
                os.Write(masterbook, 8);
                if (masterbook >= codebookCount) throw new InvalidDataException("floor names a masterbook that does not exist");
            }
            for (uint k = 0; k < (1u << (int)subclasses); k++)
            {
                uint bookPlus1 = ss.Read(8);
                os.Write(bookPlus1, 8);
                if (bookPlus1 > 0 && bookPlus1 - 1 >= codebookCount)
                    throw new InvalidDataException("floor names a subclass book that does not exist");
            }
        }

        os.Write(ss.Read(2), 2);                           // multiplier - 1
        uint rangebits = ss.Read(4);
        os.Write(rangebits, 4);
        for (uint j = 0; j < partitions; j++)
            for (uint k = 0; k < classDimensions[partitionClass[j]]; k++)
                os.Write(ss.Read((int)rangebits), (int)rangebits);
    }

    private static void RebuildResidue(BitReader ss, OggWriter os, uint codebookCount)
    {
        uint residueType = ss.Read(2);                     // packed in 2 bits, written out in 16
        os.Write(residueType, 16);
        if (residueType > 2) throw new InvalidDataException($"residue type {residueType} is not valid");

        uint begin = ss.Read(24), end = ss.Read(24), partitionSizeLess1 = ss.Read(24);
        uint classificationsLess1 = ss.Read(6), classbook = ss.Read(8);
        os.Write(begin, 24); os.Write(end, 24); os.Write(partitionSizeLess1, 24);
        os.Write(classificationsLess1, 6); os.Write(classbook, 8);
        if (classbook >= codebookCount) throw new InvalidDataException("residue names a classbook that does not exist");

        uint classifications = classificationsLess1 + 1;
        var cascade = new uint[classifications];
        for (uint j = 0; j < classifications; j++)
        {
            uint high = 0;
            uint low = ss.Read(3);
            os.Write(low, 3);
            bool bitflag = ss.ReadBit();
            os.PutBit(bitflag);
            if (bitflag) { high = ss.Read(5); os.Write(high, 5); }
            cascade[j] = high * 8 + low;
        }
        for (uint j = 0; j < classifications; j++)
            for (int k = 0; k < 8; k++)
                if ((cascade[j] & (1 << k)) != 0)
                {
                    uint book = ss.Read(8);
                    os.Write(book, 8);
                    if (book >= codebookCount) throw new InvalidDataException("residue names a book that does not exist");
                }
    }

    private static void RebuildMapping(BitReader ss, OggWriter os, int channels, uint floorCount, uint residueCount)
    {
        os.Write(0, 16);                                   // mapping type; only 0 exists

        bool submapsFlag = ss.ReadBit();
        os.PutBit(submapsFlag);
        uint submaps = 1;
        if (submapsFlag)
        {
            uint submapsLess1 = ss.Read(4);
            os.Write(submapsLess1, 4);
            submaps = submapsLess1 + 1;
        }

        bool squarePolar = ss.ReadBit();
        os.PutBit(squarePolar);
        if (squarePolar)
        {
            uint stepsLess1 = ss.Read(8);
            os.Write(stepsLess1, 8);
            int bits = WwiseCodebookLibrary.ILog((uint)(channels - 1));
            for (uint j = 0; j <= stepsLess1; j++)
            {
                uint magnitude = ss.Read(bits), angle = ss.Read(bits);
                os.Write(magnitude, bits); os.Write(angle, bits);
                if (angle == magnitude || magnitude >= (uint)channels || angle >= (uint)channels)
                    throw new InvalidDataException("mapping has an invalid channel coupling");
            }
        }

        // A reserved field Wwise happens not to strip.
        uint reserved = ss.Read(2);
        os.Write(reserved, 2);
        if (reserved != 0) throw new InvalidDataException("mapping reserved field is not zero");

        if (submaps > 1)
            for (int j = 0; j < channels; j++)
            {
                uint mux = ss.Read(4);
                os.Write(mux, 4);
                if (mux >= submaps) throw new InvalidDataException("mapping mux is out of range");
            }

        for (uint j = 0; j < submaps; j++)
        {
            os.Write(ss.Read(8), 8);                       // another unused time-domain placeholder
            uint floor = ss.Read(8);
            os.Write(floor, 8);
            if (floor >= floorCount) throw new InvalidDataException("mapping names a floor that does not exist");
            uint residue = ss.Read(8);
            os.Write(residue, 8);
            if (residue >= residueCount) throw new InvalidDataException("mapping names a residue that does not exist");
        }
    }

    // ------------------------------------------------------------------ audio packets

    /// <summary>
    /// Re-frames the audio packets and puts back the bits Wwise removed from the first byte of each.
    ///
    /// Wwise drops the leading packet-type bit and, for long-window packets, the two window bits that say
    /// what the neighbouring windows were. The type is always 0 for audio. The window bits have to be
    /// recovered by remembering the previous packet's mode and looking ahead to the next packet's, which is
    /// why this walks the stream one packet at a time rather than copying it.
    /// </summary>
    private static void WriteAudio(OggWriter os, WwiseMedia m, WwiseVorbisHeader v, bool[] modeBlockFlag)
    {
        var span = m.Data.Span;
        int end = m.Data.Length;
        int modeBits = WwiseCodebookLibrary.ILog((uint)(modeBlockFlag.Length - 1));
        bool prevBlockFlag = false;

        // Granule positions have to be computed, not copied: Wwise strips them, so every packet header in
        // this shape carries only a size. Without them a decoder cannot tell how long the stream is, and
        // NVorbis in particular will not produce samples from a stream whose final granule is zero. This is
        // the job the separate "revorb" tool does after ww2ogg; doing it inline keeps it to one pass.
        //
        // Vorbis outputs (previous blocksize + current blocksize) / 4 samples per packet, and the first
        // packet outputs nothing because it has no predecessor to overlap with.
        int blockSize0 = 1 << v.BlockSize0Pow, blockSize1 = 1 << v.BlockSize1Pow;
        long granule = 0;
        int prevSize = 0;

        int offset = (int)v.FirstAudioPacketOffset;
        while (offset < end)
        {
            if (offset + 2 > end) throw new InvalidDataException("an audio packet header is truncated");
            int size = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2));
            int payload = offset + 2;
            int next = payload + size;
            if (next > end) throw new InvalidDataException("an audio packet runs past the end of the data");

            // Work out this packet's mode before emitting, so the page can carry a real granule.
            if (size > 0)
            {
                var peek = new BitReader(m.Data.Slice(payload, size));
                uint peekMode = peek.Read(modeBits);
                if (peekMode < (uint)modeBlockFlag.Length)
                {
                    int cur = modeBlockFlag[peekMode] ? blockSize1 : blockSize0;
                    if (prevSize != 0) granule += (prevSize + cur) / 4;
                    prevSize = cur;
                }
            }
            os.SetGranule((uint)Math.Min(granule, uint.MaxValue - 1));

            if (size > 0 && !v.ModPackets)
            {
                // Nothing was stripped, so the packet is copied through byte for byte.
                for (int i = 0; i < size; i++) os.Write(span[payload + i], 8);
            }
            else if (size > 0)
            {
                var bits = new BitReader(m.Data.Slice(payload, size));
                os.PutBit(false);                          // packet type: audio

                uint modeNumber = bits.Read(modeBits);
                os.Write(modeNumber, modeBits);
                uint remainder = bits.Read(8 - modeBits);

                if (modeNumber >= (uint)modeBlockFlag.Length)
                    throw new InvalidDataException($"audio packet names mode {modeNumber}, which does not exist");

                if (modeBlockFlag[modeNumber])
                {
                    // A long window needs to know both neighbours, so peek at the next packet's mode.
                    bool nextBlockFlag = false;
                    if (next + 2 <= end)
                    {
                        int nextSize = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(next, 2));
                        if (nextSize > 0 && next + 2 + nextSize <= end)
                        {
                            var nextBits = new BitReader(m.Data.Slice(next + 2, nextSize));
                            uint nextMode = nextBits.Read(modeBits);
                            if (nextMode < (uint)modeBlockFlag.Length) nextBlockFlag = modeBlockFlag[nextMode];
                        }
                    }
                    os.PutBit(prevBlockFlag);
                    os.PutBit(nextBlockFlag);
                }
                prevBlockFlag = modeBlockFlag[modeNumber];

                os.Write(remainder, 8 - modeBits);
                for (int i = 1; i < size; i++) os.Write(span[payload + i], 8);
            }

            offset = next;
            os.FlushPage(false, offset >= end);
        }
    }
}
