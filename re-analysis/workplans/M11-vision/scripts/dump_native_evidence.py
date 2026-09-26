#!/usr/bin/env python3
"""Create a reproducible, read-only M11 native-evidence snapshot.

Requires Python 3 and LIEF. The script reads the supplied native binary and
vision configuration; it writes only the requested output file.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import lief


POINTS = {
    "VisionSystem.ctor": 0x006AFFD0,
    "VisionSystem.Init": 0x006B0658,
    "VisionSystem.EnableMode": 0x006B19C0,
    "VisionSystem.UpdatePoseData": 0x006B24D0,
    "VisionSystem.DetectFaces": 0x006B3548,
    "VisionSystem.DetectPets": 0x006B3940,
    "VisionSystem.DetectMotion": 0x006B3B90,
    "VisionSystem.DetectMarkersWithCLAHE": 0x006B4780,
    "VisionSystem.UpdateEncoded": 0x006B4B68,
    "VisionSystem.UpdateImageCache": 0x006B4D5C,
    "VisionSystem.ShouldProcessVisionMode": 0x006B5AA4,
    "VisionComponent.SetNextImage": 0x00652B04,
    "VisionComponent.WasRotatingTooFast": 0x0065359C,
    "VisionComponent.UpdateVisionSystem": 0x00653D28,
    "VisionComponent.UpdateAllResults": 0x006542EC,
    "VisionComponent.UpdateVisionMarkers": 0x00654D60,
    "VisionComponent.AddLiftOccluder": 0x006564D8,
    "RobotStateHistory.CullToWindowSize": 0x005309D0,
    "RobotStateHistory.GetRawStateAt": 0x00531430,
    "RobotStateHistory.ComputeStateAt": 0x00531784,
    "BlockWorld.AddAndUpdateObjects": 0x00620AD4,
    "BlockWorld.CheckForUnobservedObjects": 0x00621C6C,
    "BlockWorld.OnRobotDelocalized": 0x006249C4,
    "BlockWorld.UpdateObservedMarkers": 0x00624EE8,
    "BlockWorld.CreateObjectsFromMarkers": 0x0062539C,
    "ObjectPoseConfirmer.UpdatePoseInInstance": 0x00505DE0,
    "ObjectPoseConfirmer.AddVisualObservation": 0x0050684C,
    "ObjectPoseConfirmer.MarkObjectUnobserved": 0x00506FBC,
    "ObjectPoseConfirmer.MarkObjectDirty": 0x005075A4,
    "Camera.ComputeObjectPoseHelper": 0x0085D750,
    "Camera.solvePnP_callsite": 0x0085D8A2,
    "Camera.AddOccluderKnownMarker": 0x0085E76C,
    "KnownMarker.IsVisibleFrom": 0x0087E4A8,
    "Marker.GetNearestNeighborLibrary": 0x0089ED1C,
    "Marker.GetProbeValues": 0x0089EF40,
    "Marker.ComputeBrightDarkValues": 0x0089F8E8,
    "Marker.RefineCorners": 0x0089FD98,
    "Marker.DetectFiducialMarkers": 0x00898760,
    "Marker.CharacteristicScale": 0x00890448,
    "Marker.BinomialFilter": 0x008A2344,
    "Marker.IsQuadrilateralReasonable": 0x00892B18,
    "Marker.ComputeQuadrilaterals": 0x00892D70,
    "Marker.ExtractLineFitsPeaks": 0x008A5DB8,
    "Marker.RefineQuadrilateral": 0x008C55E0,
    "Marker.TraceBoundary": 0x008C6B18,
    "OverheadEdgesDetector.Detect": 0x006ABE34,
    "MapComponent.ProcessVisionOverheadEdges": 0x0067F7AC,
    "MapComponent.AddVisionOverheadEdges": 0x0067F814,
    "MotionDetector.Detect": 0x006AAAF0,
    "FaceTracker.Impl.Update": 0x0086D740,
    "PetTracker.Update": 0x0087C980,
}


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def containing_symbol(symbols, address: int):
    candidates = []
    for symbol in symbols:
        start = int(symbol.value) & ~1
        if start <= address and int(symbol.size) > 0 and address < start + int(symbol.size):
            candidates.append(symbol)
    return min(candidates, key=lambda s: int(s.size)) if candidates else None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--so", required=True, type=Path)
    parser.add_argument("--vision-config", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    binary = lief.parse(str(args.so))
    if binary is None:
        raise SystemExit(f"could not parse {args.so}")
    symbols = list(binary.symbols)
    points = []
    for label, address in POINTS.items():
        sym = containing_symbol(symbols, address & ~1)
        start = (int(sym.value) & ~1) if sym else (address & ~1)
        size = int(sym.size) if sym else 0
        # A short local digest makes accidental binary/address drift visible even
        # for an instruction-level call site rather than a function entry.
        local = bytes(binary.get_content_from_virtual_address(address & ~1, 32))
        points.append({
            "label": label,
            "address": f"0x{address & ~1:08x}",
            "containing_symbol": sym.name if sym else None,
            "symbol_address": f"0x{start:08x}" if sym else None,
            "symbol_size": size if sym else None,
            "bytes_32_sha256": hashlib.sha256(local).hexdigest(),
        })

    document = {
        "format": "cozmo-m11-native-evidence-v1",
        "inputs": {
            "libcozmoEngine.so": {"sha256": sha256(args.so), "size": args.so.stat().st_size},
            "vision_config.json": {"sha256": sha256(args.vision_config), "size": args.vision_config.stat().st_size},
        },
        "notes": [
            "Addresses are ARM Thumb addresses normalized to an even value.",
            "A local byte digest detects binary drift; it does not interpret behavior.",
            "Behavior claims remain in NATIVE-EVIDENCE.md and require disassembly review.",
        ],
        "points": points,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
