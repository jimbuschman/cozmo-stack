#!/bin/sh
set -eu
cd "$(dirname "$0")"
D="$PWD/_jsoncpp_qemu_dependencies/extracted"
export LD_LIBRARY_PATH="$D/usr/lib/x86_64-linux-gnu${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
for alias in strict-aliasing no-strict-aliasing; do
 "$D/usr/bin/arm-linux-gnueabi-gcc-12" -B"$D/usr/lib/gcc-cross/arm-linux-gnueabi/12/" -B"$D/usr/bin/" -O2 -mthumb -march=armv7-a -mfpu=vfpv3-d16 -mfloat-abi=softfp -f$alias -S 20261005-converter-ratio-compiler-probe.c -o "20261005-converter-ratio-$alias.s"
done
