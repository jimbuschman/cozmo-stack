"""Exercise the colour camera over a local Cozmo MCP server."""
import argparse
import base64
import hashlib
import json
from datetime import datetime
from pathlib import Path
from queue import Empty, Queue
import subprocess
from threading import Thread
from time import monotonic, sleep


class McpClient:
    def __init__(self, command, log):
        self.process = subprocess.Popen(
            command, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=log,
            text=True, encoding="utf-8",
        )
        self.responses = Queue()
        self.next_id = 0
        Thread(target=self.read_responses, daemon=True).start()

    def read_responses(self):
        for line in self.process.stdout:
            self.responses.put(line)
        self.responses.put(None)

    def send(self, **message):
        self.process.stdin.write(json.dumps({"jsonrpc": "2.0", **message}) + "\n")
        self.process.stdin.flush()

    def request(self, method, params, timeout=8):
        self.next_id += 1
        self.send(id=self.next_id, method=method, params=params)
        deadline = monotonic() + timeout
        while monotonic() < deadline:
            try:
                row = self.responses.get(timeout=max(0, deadline - monotonic()))
            except Empty:
                break
            if row is None:
                raise RuntimeError("Cozmo MCP exited. See engine.stderr.log.")
            response = json.loads(row)
            if response.get("id") == self.next_id:
                if "error" in response:
                    raise RuntimeError(response["error"])
                return response["result"]
        raise TimeoutError(f"Cozmo MCP timed out on {method}.")

    def tool(self, name, **arguments):
        return self.request("tools/call", {"name": name, "arguments": arguments})

    def close(self):
        self.process.stdin.close()
        try:
            self.process.wait(timeout=12)
        except subprocess.TimeoutExpired:
            self.process.terminate()
            self.process.wait(timeout=5)


def configure_color(client, enabled):
    result = client.tool("set_camera_color", enabled=enabled)
    if result.get("isError") or result["structuredContent"].get("status") != "confirmed":
        raise RuntimeError(result)
    return result["structuredContent"]["report"]


def grab(client, output, label, old_id=None, expected_color=None):
    deadline = monotonic() + 15
    latest = None
    while monotonic() < deadline:
        frame = client.tool("get_camera_image", annotate=True)
        if not frame.get("isError"):
            data = frame["structuredContent"]
            if data["imageId"] != old_id:
                latest = frame
                has_cube = any(box["type"].startswith("Block_LIGHTCUBE") for box in data["objects"])
                if expected_color is None or data["isColor"] == expected_color and (not expected_color or has_cube):
                    break
        sleep(0.25)
    if latest is None:
        return {"observation": "No fresh camera image"}
    image = next(item for item in latest["content"] if item["type"] == "image")
    file = label + ".png"
    (output / file).write_bytes(base64.b64decode(image["data"], validate=True))
    return {"file": file, **latest["structuredContent"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mcp", required=True, type=Path, help="Path to built cozmo-mcp.dll")
    parser.add_argument("--resources", required=True, type=Path)
    parser.add_argument("--robot-ip", default="172.31.1.1")
    parser.add_argument("--out", type=Path)
    parser.add_argument("--validate", action="store_true", help="Validate configuration without connecting")
    options = parser.parse_args()
    output = options.out or Path(__file__).resolve().parents[1] / (
        datetime.now().strftime("%Y%m%d-%H%M%S") + "-COLOR-CAMERA-1"
    )
    command = [
        "dotnet", str(options.mcp.resolve()), "--resources", str(options.resources.resolve()),
        "--robot-ip", options.robot_ip, "--out", str(output.resolve()),
    ]
    if options.validate:
        return subprocess.run([*command, "--validate"], check=False).returncode

    output.mkdir(parents=True, exist_ok=False)
    summary = {"scriptSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
    print("Put a cube and a colourful object in front of Cozmo, with the cube marker facing him.")
    print(f"Evidence will be saved in {output.resolve()}")
    try:
        with (output / "engine.stderr.log").open("w", encoding="utf-8") as log:
            client = McpClient(command, log)
            try:
                client.request("initialize", {
                    "protocolVersion": "2025-11-25", "capabilities": {},
                    "clientInfo": {"name": "color-camera-hw", "version": "1"},
                })
                client.send(method="notifications/initialized", params={})
                deadline = monotonic() + 150
                while monotonic() < deadline:
                    state = client.tool("get_state")["structuredContent"]
                    if state["connection"]["ready"]:
                        break
                    sleep(0.5)
                else:
                    raise TimeoutError("Cozmo did not become ready.")
                summary["before"] = grab(client, output, "before")
                try:
                    summary["enable"] = configure_color(client, True)
                    summary["color"] = grab(client, output, "colour", summary["before"].get("imageId"), True)
                finally:
                    summary["disable"] = configure_color(client, False)
                summary["after"] = grab(client, output, "after",
                    summary.get("color", {}).get("imageId", summary["before"].get("imageId")), False)
            finally:
                client.close()
        summary["status"] = "observations-recorded"
        print("Compare before.png, colour.png, and after.png when present. Check for colour and a matching cube box.")
        return 0
    except (OSError, ValueError, RuntimeError, TimeoutError) as error:
        summary["status"] = "inconclusive"
        summary["error"] = str(error)
        print(error)
        return 1
    finally:
        (output / "color-camera-result.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    raise SystemExit(main())
