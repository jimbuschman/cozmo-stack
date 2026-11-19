# COLOR-CAMERA-1 handoff

Use MCP `set_camera_color` with `enabled: true`, then `get_camera_image` with `annotate: true`. The setting confirms the request. The image has the received frame's `isColor` and effective `encoding` alongside its PNG and matching detection boxes. Use `enabled: false` to request greyscale again.

| Input | Engine path and visible result |
| --- | --- |
| Colour toggle | Unity `Anki.Cozmo.ExternalInterface/EnableColorImages.cs` packs one bool under G2E 0x77. Its handler at `0x0065835C–0x00658378` and `EnableColorImages` at `0x006582CC–0x006582E6` store VisionComponent+0x32A and call `0x006582E8`. That helper constructs robot tag 0x66 and sends reliable=1, hot=0 at `0x00658308–0x00658314`. It sends only the colour flag. The running stream keeps its resolution and image request. MCP calls the existing CameraSettings sender on the engine owner. |
| Image chunks | `0x004F1D7A–0x004F1D9C` reads the first chunk's encoding and first data byte. Encoding 8 with a nonzero colour flag becomes effective encoding 9. `0x004F237E–0x004F2440` reconstructs its 160-wide JPEG with the colour header, decodes with flags=1, converts BGR to RGB via code 4, and uses linear resize to the 320×240 output. Each incoming frame selects its decoder independently of the requested flag. |
| Vision and presentation | `0x006B4B7C–0x006B4B9C` selects RGB decode and RGB cache reset. `0x006B4D94` gets the grey image for the detection loop (M11-033, markers and faces in `0x006B5122–0x006B55E4`). `ImageRGB::FillGray` at `0x00872998–0x008729CA` uses OpenCV RGB2GRAY (7). The RGB cache remains available to colour consumers, including DetectingLaserPoints at `0x006B5552–0x006B55E2` → `0x006A7A20`. MCP uses the RGB decode for PNG and draws existing exact-timestamp boxes. |

The historical firmware 2457 capture `20261012-062328-M3M4-HW` documented wire encoding 8 and first payload byte 1 for colour, yielding effective encoding 9. After the enable, the first frame was still grey and 57 of 58 frames were colour. The recorded colour scan 02350 replayed as three chunks, reconstructed the 160-wide JPEG and produced a colour 320×240 PNG. Synthetic RawRGB chunks preserved a real detected and confirmed cube with an annotated PNG. Tinted RGB inputs from the previously captured face replay also reached the stock face detector and produced a timestamp-matched face box. The wire toggle test checks exactly 66 01 and 66 00 and checks for no extra ImageRequest.

M3-016 remains HARDWARE_ONLY. M3-023 and the existing decoder/vision records keep their current classifications. This replay uses the historical scan and assembled synthetic chunks. The operator run will record what the current robot sends, including any change in format or transition frames.

## Operator round

Place a cube and another colourful object in front of Cozmo with the cube marker facing him. Run from the repository with a built MCP executable and the robot connected to the PC. The script waits for readiness and takes baseline, colour, and greyscale PNGs. Most of the round is automatic and the three image waits total up to 45 seconds. Compare the images for colour and a matching cube box. The evidence folder contains `color-camera-result.json` and the raw MCP capture. The `--validate` option checks launch configuration without connecting.

```powershell
python re-analysis/acceptance/hardware/scripts/color-camera-hw.py --mcp cozmo-stack/src/Cozmo.Mcp/bin/Debug/net9.0/cozmo-mcp.dll --resources re-analysis/obb/assets/cozmo_resources
```

**Full clean 8771/8771** on d4e6632e in 47.4515 minutes. Gate 2c6aa910bd72542f5b8dcc5aed310bf594e8ec7f. Focused suite 381/381. The script validated with no robot connection or bundle directory. Fidelity and diff checks passed.
