#!/bin/sh
set -eu
cd "$(dirname "$0")"
D="$PWD/_jsoncpp_qemu_dependencies/extracted"
export LD_LIBRARY_PATH="$D/usr/lib/x86_64-linux-gnu${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
"$D/usr/bin/arm-linux-gnueabi-gcc-12" -B"$D/usr/lib/gcc-cross/arm-linux-gnueabi/12/" -B"$D/usr/bin/" -O2 -marm -march=armv7-a -mfpu=vfpv3-d16 -mfloat-abi=softfp -ffreestanding -fno-builtin -fno-stack-protector -nostdlib -static -Wl,-e,_start -Wl,--build-id=none 20261005-jsoncpp-qemu.c -o 20261005-jsoncpp-qemu-arm
"$D/usr/bin/qemu-arm" -cpu cortex-a9 ./20261005-jsoncpp-qemu-arm ../../resources/lib/armeabi-v7a/libc++_shared.so "${1:-131.e-227}"
