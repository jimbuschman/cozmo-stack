"""Reproduce the bounded check-B native evidence. Requires lief, capstone, cpp-demangle.

python re-analysis/tools/recover_cube_connection.py --so PATH

Ranges cover the normal control-flow bodies, including early-return alternatives, but omit
exception cleanup and trailing literal pools. Call-site scanning is over exported function
bodies and resolves direct/PLT targets; absence is NOT proof against inline/indirect calls.
"""
import argparse
import hashlib
from pathlib import Path
import capstone
import cpp_demangle
import lief

RANGES = {
    "Robot::ConnectToRequestedObjects()": (0x514a70, 0x514c8e),
    "Robot::ConnectToObjects(": (0x5173ea, 0x517488),
    "Robot::CheckDisconnectedObjects()": (0x514924, 0x514a1e),
    "Robot::HandleConnectedToObject(": (0x5179c0, 0x517b0a),
    "Robot::HandleDisconnectedFromObject(": (0x517bbc, 0x517d20),
    "Robot::GetClosestDiscoveredObjectsOfType(": (0x518314, 0x518342),
    "Robot::Update()": (0x514174, 0x51423a),
    "RobotToEngineImplMessaging::HandleActiveObjectAvailable(": (0x53391c, 0x533a68),
    "RobotToEngineImplMessaging::HandleActiveObjectConnectionState(": (0x533b3c, 0x533d32),
    "BlockFilter::HandleGameEvents(": (0x619e7c, 0x61a160),
    "BlockFilter::Init(": (0x61a1ec, 0x61a282),
    "BlockFilter::Update()": (0x61a79a, 0x61a7ec),
    "BlockFilter::UpdateDiscovering()": (0x61a7ec, 0x61a9d4),
    "BlockFilter::UpdateConnecting()": (0x61aa8c, 0x61ab9a),
    "BlockFilter::AddObjectToPersistentPool(": (0x61ac5c, 0x61adac),
    "BlockFilter::Enable(": (0x61b59c, 0x61b62a),
    "Robot::SendMessage(": (0x51349c, 0x513528),
    "RobotInterface::MessageHandler::SendMessage(": (0x69dcf6, 0x69dd78),
    "SetPropSlot::Pack(CLAD": (0x7a11a4, 0x7a11d6),
}

def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--so", required=True, type=Path)
    args = ap.parse_args()
    raw = args.so.read_bytes()
    expected_hash = "02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1"
    if hashlib.sha256(raw).hexdigest() != expected_hash:
        raise ValueError("instruction ranges apply only to the recorded 3.4.0-1204 ARM binary")
    so = lief.ELF.parse(str(args.so))
    def dem(name):
        try:
            return cpp_demangle.demangle(name)
        except Exception:
            return name
    symbols = {s.value & ~1: s for s in so.dynamic_symbols if s.value and s.size and s.is_function}
    names = {a: dem(s.name) for a, s in symbols.items()}
    targets = dict(names)
    plt = so.get_section(".plt")
    for i, relocation in enumerate(so.pltgot_relocations):
        if relocation.has_symbol:
            targets[plt.virtual_address + 20 + 12 * i] = dem(relocation.symbol.name)
    md = capstone.Cs(capstone.CS_ARCH_ARM, capstone.CS_MODE_THUMB)
    def instructions(start, end):
        offset = so.virtual_address_to_offset(start)
        return md.disasm(raw[offset:offset + end - start], start)
    def target(ins):
        if ins.mnemonic in ("bl", "blx", "b", "b.w") and ins.op_str.startswith("#"):
            return targets.get(int(ins.op_str[1:], 16), "")
        return ""
    out = ["Cozmo 3.4.0-1204 ARM native cube-connection evidence",
           "SHA256 " + hashlib.sha256(raw).hexdigest(),
           "Ranges omit exception cleanup/literal pools; jump-table data inside bodies may decode as instructions.",
           "Direct/PLT call sites (inline/indirect calls are not excluded by this search):"]
    terms = ("SetPropSlot&&", "Robot::ConnectToObjects(", "Robot::ConnectToRequestedObjects()",
             "SetAccessoryDiscovery", "setAccessoryDiscovery", "BlockFilter::Enable(")
    for address, symbol in sorted(symbols.items()):
        for ins in instructions(address, address + symbol.size):
            name = target(ins)
            if any(term in name for term in terms):
                out.append(f"{ins.address:08x} {names[address]} -> {name}")
    for term, (start, end) in RANGES.items():
        matches = [a for a, name in names.items() if term in name and a <= start < a + symbols[a].size]
        if len(matches) != 1:
            raise ValueError(f"unexpected binary: {term}: {matches}")
        out.append(f"\n{names[matches[0]]} [{start:08x}, {end:08x})")
        for ins in instructions(start, end):
            out.append(f"{ins.address:08x} {ins.mnemonic:8} {ins.op_str} ; {target(ins)}")
    path = Path(__file__).resolve().parents[1] / "disassembly" / "dis_cube_connection.txt"
    path.write_text("\n".join(out) + "\n", encoding="utf-8")
    print(path)

if __name__ == "__main__":
    main()
