"""Check the MCP backpack and cube light packets after Cozmo connects."""
import argparse
from datetime import datetime
import hashlib
import json
from pathlib import Path
import runpy
import struct
import subprocess
from time import monotonic, sleep

SCRIPT = Path(__file__).resolve()
RECORDS = ("M2-005", "M4-017", "M4-018", "M4-036", "M4-044")
BACKPACK = dict(ledMask=18, onColor="123456", offColor="00FF00", onMs=31, offMs=60,
                transitionOnMs=1, transitionOffMs=91)
CUBE = dict(ledMask=5, onColor="FF6432", offColor="0080FF", onMs=31, offMs=60,
            transitionOnMs=1, transitionOffMs=91)


def expected_packets(slot):
    # M4-017 0x00631F3A..0x006321BE, M2-005 LightState.
    body_light = bytes.fromhex("CA88E083020201040000")
    off = bytes(10)
    middle = b"\x03" + body_light + off * 2 + b"\x00"
    arrows = b"\x11" + off + body_light + b"\x00"
    # M4-018 0x004E48B0..0x004E49A2 and 0x0063A654..0x0063A800.
    cube_light = bytes.fromhex("E3FC1F82020201040000")
    cube_off = bytes.fromhex("00000000454500000000")
    select = b"\x10" + struct.pack("<IB", slot, 0)
    cube = b"\x04" + (cube_light + cube_off) * 2
    return middle, arrows, select, cube


def check_robot_packets(output, slot):
    middle, arrows, select, cube = expected_packets(slot)
    expected = {"backpackMiddle03": middle, "backpackArrows11": arrows, "cubeLights04": cube}
    seen = {name: False for name in expected}
    selected = False
    for line in (output / "messages.jsonl").read_text(encoding="utf-8").splitlines():
        row = json.loads(line)
        if not row["outbound"]:
            continue
        payload = bytes.fromhex(row["payloadHex"])
        if payload[:1] == b"\x10":
            selected = payload == select
        for name, target in expected.items():
            if payload == target and (name != "cubeLights04" or selected):
                seen[name] = True
    return seen


def submitted(client, tool, **args):
    reply = client.tool(tool, **args)
    if reply.get("isError") or reply["structuredContent"]["status"] != "submitted":
        raise RuntimeError(reply)
    return reply["structuredContent"]["report"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mcp", required=True, type=Path)
    parser.add_argument("--resources", required=True, type=Path)
    parser.add_argument("--robot-ip", default="172.31.1.1")
    parser.add_argument("--cube-id", type=int, help="Use this connected world ID when multiple cubes are present")
    parser.add_argument("--out", type=Path)
    parser.add_argument("--validate", action="store_true", help="Validate configuration without connecting")
    args = parser.parse_args()
    output = args.out or SCRIPT.parents[1] / (datetime.now().strftime("%Y%m%d-%H%M%S") + "-LIGHTS-MCP-1")
    output = output.resolve()
    command = ["dotnet", str(args.mcp.resolve()), "--resources", str(args.resources.resolve()),
               "--robot-ip", args.robot_ip, "--out", str(output)]
    if args.validate:
        manifest = json.loads((SCRIPT.parents[3] / "fidelity_manifest.json").read_text(encoding="utf-8"))
        existing = {record["id"] for record in manifest["records"]}
        if not set(RECORDS) <= existing:
            raise ValueError("A light test fidelity reference is missing")
        if tuple(map(len, expected_packets(2))) != (32, 22, 6, 41):
            raise ValueError("A light packet expectation has an invalid wire size")
        print("Checked packet sizes and fidelity references.")
        return subprocess.run([*command, "--validate"], check=False).returncode

    output.mkdir(parents=True, exist_ok=False)
    summary = {"fidelity": RECORDS, "scriptSha256": hashlib.sha256(SCRIPT.read_bytes()).hexdigest()}
    print("Place Cozmo on a clear desk with one connected cube. Watch the backpack and cube LEDs.")
    try:
        with (output / "engine.stderr.log").open("w", encoding="utf-8") as log:
            McpClient = runpy.run_path(str(SCRIPT.with_name("color-camera-hw.py")))["McpClient"]
            client = McpClient(command, log)
            try:
                client.request("initialize", {
                    "protocolVersion": "2025-11-25", "capabilities": {},
                    "clientInfo": {"name": "lights-hw", "version": "1"},
                })
                client.send(method="notifications/initialized", params={})
                deadline = monotonic() + 60
                while monotonic() < deadline:
                    state = client.tool("get_state")["structuredContent"]
                    cubes = [c for c in state["knownCubes"]
                             if c["connected"] and c.get("worldObjectId") is not None
                             and c.get("radioObjectId") is not None
                             and (args.cube_id is None or c["worldObjectId"] == args.cube_id)]
                    if state["connection"]["ready"] and cubes:
                        break
                    sleep(0.4)
                else:
                    raise TimeoutError("Cozmo and the selected connected cube did not become available.")
                cube = min(cubes, key=lambda c: c["worldObjectId"])
                world_id, slot = cube["worldObjectId"], cube["radioObjectId"]
                summary["cubeWorldId"] = world_id
                summary["cubeRadioSlot"] = slot
                client.tool("freeplay", enabled=False)
                try:
                    print("Front and right backpack LEDs, then cube LEDs 0 and 2.")
                    summary["backpack"] = submitted(client, "set-backpack-lights", **BACKPACK)
                    sleep(5)
                    summary["cube"] = submitted(client, "set-cube-lights", objectId=world_id, **CUBE)
                    sleep(5)
                finally:
                    try:
                        submitted(client, "set-backpack-lights", onColor="000000")
                    finally:
                        try:
                            submitted(client, "set-cube-lights", objectId=world_id, onColor="000000", onMs=0)
                        finally:
                            if state.get("highLevelActivity") == "Freeplay":
                                client.tool("freeplay", enabled=True)
            finally:
                client.close()

        summary["wireChecks"] = check_robot_packets(output, slot)
        summary["status"] = "passed" if all(summary["wireChecks"].values()) else "wire-check-failed"
        print(f"Wire checks: {summary['wireChecks']}")
        print(f"Evidence saved in {output}. LED appearance is an operator observation.")
        return 0 if summary["status"] == "passed" else 1
    except (OSError, ValueError, RuntimeError, TimeoutError, KeyError) as error:
        summary["status"] = "inconclusive"
        summary["error"] = str(error)
        print(error)
        return 1
    finally:
        (output / "lights-result.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    raise SystemExit(main())
