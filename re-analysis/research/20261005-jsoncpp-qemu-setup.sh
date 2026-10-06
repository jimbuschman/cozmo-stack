#!/bin/sh
set -eu
cd "$(dirname "$0")/_jsoncpp_qemu_dependencies"
for package in ./*.deb; do dpkg-deb -x "$package" extracted; done
extracted/usr/bin/qemu-arm --version
