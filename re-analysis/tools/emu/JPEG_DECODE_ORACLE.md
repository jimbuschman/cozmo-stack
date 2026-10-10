# Shipped camera-decode oracle (M3-001 / M3-018)

`emu_jpeg_decode.py` runs the **shipped** decode path of the engine under Unicorn, over the libraries as they are in
`resources/lib/armeabi-v7a/` (local only; `/resources/` is not in git):

| library | what runs |
|---|---|
| `libcozmoEngine.so` | `EncodedImage::DecodeImageGray` / `DecodeImageRGB` (`DecodeImageHelper<Image>` 0x4F287C, `<ImageRGB>` 0x4F2184), `MiniToJpegHelper`, the log calls, `_errG` |
| `libopencv_imgcodecs.so` | `cv::imdecode` and OpenCV's `JpegDecoder` around the libjpeg 9 decompressor it carries (rows J5..J115) |
| `libopencv_imgproc.so` | `cv::cvtColor` (BGR2RGB), `cv::resize` (INTER_LINEAR 8U), `cv::copyMakeBorder` |
| `libopencv_core.so`, `libc++_shared.so` | `cv::Mat`, `cv::error`, the C++ library |

`emu_elf.py` is the loader: it maps the libraries side by side, applies their relocations, binds each import to the first library that
defines it, and stops (`Abort`) on an import with no definition and no stand-in, so nothing is faked silently.

```text
python re-analysis/tools/emu/emu_jpeg_decode.py payload.bin --encoding 8 [--rgb] [--out mat.raw] [--no-neon] [--poison 0xFF]
python re-analysis/tools/emu/emu_jpeg_decode.py file.jpg --imdecode 1 --out mat.raw
python re-analysis/tools/emu/gen_jpeg_decode_fixtures.py [--quick] [--no-coverage]     # the fixtures under Fixtures/jpeg_decode
```

## Boundaries (explicit fixture assumptions)

* **Not shipped, so stood in by Python:** bionic libc (malloc, string.h, printf, setjmp/longjmp, pthread keys), libm, `pthread_once`,
  `/proc/self/auxv`. `printf("%s", NULL)` renders `(null)` as bionic does; the inventory does not settle it.
* **Heap and stack are poisoned** (`--poison`, default 0x00) and the heap is reset before each call, so an uninitialised read returns the
  poison. The generator runs every case with 0x00 and 0xFF: bytes that differ are bytes the shipped code never writes (the scratch row of a
  decode that suspends before its first row, J72) and are recorded as `excluded`, not as expected values.
* **NEON** is decided by the served auxv (`HWCAP_NEON`). The generator runs every case with it present and absent; a difference would be listed
  (`neon_differences`) and fails the test that guards the corpus. None was found in this corpus (a measurement over the corpus, not a proof for every input).
* **`cv::parallel_for_` runs the loop body once over the whole range** (the partition does not change pixels, J44) and OpenCL is off.
* **A C++ throw ends the run** (`__cxa_throw` is a stop, the unwinder is not shipped here); the exception text is the library's own
  (`cv::error` formats it, so the oracle records it from the Android-log call).
* FPSCR is the reset value (round to nearest, J42's runtime assumption, the same as M1-029).

The oracle also exposes the pieces: `imdecode(buf, flags)`, `resize(...)`, `cvtcolor(...)`, and `idct(w, h, coef, quant)`, which calls any of
the 32 integer inverse DCT bodies directly (their addresses are read back from `jddctmgr`'s start_pass at 0x2A608).

`jpegcraft.py` builds the inputs (marker segments, a baseline encoder, random entropy), `jpeg_coverage.py` measures which of the code the
J rows cite the corpus executes. `jpeg_inputs/` holds the 27 real robot frames of `Fixtures/hw_fw2457_probe.log` as mini payloads
(`export_probe_frames.cs.txt` is the exporter).
