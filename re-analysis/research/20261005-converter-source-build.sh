#!/bin/sh
set -eu
cd "$(dirname "$0")"
for kind in original patched directed; do
 gcc -O2 -shared -fPIC -fno-strict-aliasing -ffp-contract=off -frounding-math -Dstrtod=research_strtod "20261005-converter-source-$kind.c" -o "20261005-converter-source-$kind.so" -pthread -lm
done
