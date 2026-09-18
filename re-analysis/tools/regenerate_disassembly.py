#!/usr/bin/env python3
"""
Regenerate the tracked disassembly excerpts from libcozmoEngine.so.

    python re-analysis/tools/regenerate_disassembly.py [--check] [--so <path>]

Each tracked file holds the disassembly of a named set of functions, chosen because a specific documented
claim rests on it. Nothing here is a bulk dump of the binary: the library exports tens of thousands of
symbols and fewer than fifty appear in this directory. The manifest below is the whole of what is kept, and
every entry names the claim it supports, so anything that stops being cited can be dropped.

Symbols are given by their demangled names and resolved against the library's own symbol table, so a
different build fails loudly instead of disassembling the wrong address.

dis_mulaw.txt is not listed here: it is produced by extract_mulaw.py, which adds the segment table, the
scale constant and a self-check around the same disassembly.
"""
import argparse
import subprocess
import sys
import tempfile
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
OUT_DIR = TOOLS.parent / "disassembly"
DEFAULT_SO = TOOLS.parent.parent / "resources" / "lib" / "armeabi-v7a" / "libcozmoEngine.so"

# file -> (what it is evidence for, [demangled symbol names])
MANIFEST = {
    "dis_robotconn.txt": (
        "TRANSPORT_SPEC and TransportOptions: the engine's reliable-transport tunables, and the 5551/5552 "
        "physical-vs-simulated port choice",
        [
            "Anki::Cozmo::RobotConnectionManager::Init()",
            "Anki::Cozmo::RobotConnectionManager::ConfigureReliableTransport()",
            "Anki::Cozmo::RobotConnectionManager::Connect(Anki::Util::TransportAddress const&)",
            "Anki::Cozmo::RobotInterface::MessageHandler::AddRobotConnection(Anki::Cozmo::ExternalInterface::ConnectToRobot const&)",
        ],
    ),
    "dis_reliabletransport.txt": (
        "TRANSPORT_SPEC: frame construction, the receive path and sub-message dispatch that ReliableTransport.cs ports",
        [
            "Anki::Util::ReliableTransport::BuildHeader(unsigned char*, unsigned int, unsigned char, unsigned short, unsigned short, unsigned short)",
            "Anki::Util::ReliableTransport::QueueMessage(bool, Anki::Util::TransportAddress const&, unsigned char const*, unsigned int, Anki::Util::EReliableMessageType, bool)",
            "Anki::Util::ReliableTransport::SendMessage(bool, Anki::Util::TransportAddress const&, unsigned char const*, unsigned int, Anki::Util::EReliableMessageType, bool, double)",
            "Anki::Util::ReliableTransport::SendData(bool, Anki::Util::TransportAddress const&, unsigned char const*, unsigned int, bool)",
            "Anki::Util::ReliableTransport::Connect(Anki::Util::TransportAddress const&)",
            "Anki::Util::ReliableTransport::FinishConnection(Anki::Util::TransportAddress const&)",
            "Anki::Util::ReliableTransport::Disconnect(Anki::Util::TransportAddress const&)",
            "Anki::Util::ReliableTransport::HandleSubMessage(unsigned char const*, unsigned int, unsigned char, unsigned short, Anki::Util::ReliableConnection*, Anki::Util::TransportAddress const&)",
            "Anki::Util::ReliableTransport::ReceiveData(unsigned char const*, unsigned int, Anki::Util::TransportAddress const&)",
            "Anki::Util::ReliableTransport::Update()",
        ],
    ),
    "dis_reliableconn.txt": (
        "TRANSPORT_SPEC: the reliability state machine ReliableConnection.cs ports, including resend and timeout rules",
        [
            "Anki::Util::ReliableConnection::AckMessage(unsigned short)",
            "Anki::Util::ReliableConnection::AddMessage(Anki::Util::SrcBufferSet const&, Anki::Util::EReliableMessageType, unsigned short, bool, double)",
            "Anki::Util::ReliableConnection::ReceivePing(Anki::Util::ReliableTransport*, unsigned char const*, unsigned int)",
            "Anki::Util::ReliableConnection::SendUnAckedPackets(Anki::Util::ReliableTransport*, unsigned int, unsigned int)",
            "Anki::Util::ReliableConnection::SendOptimalUnAckedPackets(Anki::Util::ReliableTransport*, unsigned int)",
            "Anki::Util::ReliableConnection::Update(Anki::Util::ReliableTransport*)",
            "Anki::Util::ReliableConnection::SendPing(Anki::Util::ReliableTransport*, double, bool)",
            "Anki::Util::ReliableConnection::IsNextInSequenceId(unsigned short) const",
            "Anki::Util::ReliableConnection::IsPacketWorthSending(Anki::Util::ReliableTransport const*, double, unsigned int) const",
            "Anki::Util::ReliableConnection::HasConnectionTimedOut() const",
        ],
    ),
    "dis_udptransport.txt": (
        "TRANSPORT_SPEC: the COZ prefix, the absence of a CRC and the 1420-byte maximum message size",
        [
            "Anki::Util::UDPTransport::OpenSocket(int)",
            "Anki::Util::UDPTransport::HeaderPrefix::Set(unsigned char const*, unsigned int)",
            "Anki::Util::UDPTransport::GetHeaderSize()",
            "Anki::Util::UDPTransport::SetHeaderPrefix(unsigned char const*, unsigned int)",
            "Anki::Util::UDPTransport::TryToReadMessage()",
            "Anki::Util::UDPTransport::GetMaxNetMessageSize()",
            "Anki::Util::UDPTransport::SetDoesHeaderHaveCRC(bool)",
            "Anki::Util::UDPTransport::HandleReceivedMessage(unsigned char const*, unsigned int, Anki::Util::TransportAddress const&, bool)",
            "Anki::Util::UDPTransport::SendData(Anki::Util::TransportAddress const&, Anki::Util::SrcBufferSet const&)",
        ],
    ),
    "dis_cozmo_startup.txt": (
        "README section 2: the startup JSON keys the engine reads",
        ["cozmo_startup"],
    ),
}


def mangled_index(so_path):
    import lief
    import cpp_demangle
    so = lief.ELF.parse(str(so_path))
    if so is None:
        sys.exit(f"not an ELF file: {so_path}")
    index = {}
    for s in so.dynamic_symbols:
        if not s.value:
            continue
        try:
            index.setdefault(cpp_demangle.demangle(s.name), s.name)
        except Exception:
            index.setdefault(s.name, s.name)
    return index


def build(so_path, name, symbols, index):
    mangled = []
    for want in symbols:
        m = index.get(want)
        if m is None:
            sys.exit(f"{name}: {want!r} is not exported by {so_path}; this is not the build it was taken from")
        mangled.append(m)
    r = subprocess.run([sys.executable, str(TOOLS / "disarm.py"), str(so_path), *mangled],
                       capture_output=True, text=True)
    if r.returncode != 0:
        sys.stderr.write(r.stdout + r.stderr)
        sys.exit(f"{name}: disarm.py failed")
    return r.stdout


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--so", default=str(DEFAULT_SO))
    ap.add_argument("--check", action="store_true", help="compare with what is committed instead of writing")
    args = ap.parse_args()

    so_path = Path(args.so)
    if not so_path.exists():
        sys.exit(f"{so_path} is not present. The decompiled APK is not redistributed; unpack it locally first.")

    index = mangled_index(so_path)
    differing = []
    for name, (why, symbols) in MANIFEST.items():
        text = build(so_path, name, symbols, index)
        target = OUT_DIR / name
        if args.check:
            if not target.exists() or target.read_text(encoding="utf-8") != text:
                differing.append(name)
        else:
            target.write_text(text, encoding="utf-8")
            print(f"{name}: {len(symbols)} function(s) -- {why}")

    if args.check:
        if differing:
            print("these do not match what the binary produces: " + ", ".join(differing))
            sys.exit(1)
        print("all tracked disassembly matches the binary")


if __name__ == "__main__":
    main()
