#!/usr/bin/env python3
"""
Rebuild everything generated from the canonical protocol definition, in order, and check the result.

    python re-analysis/tools/regenerate_protocol.py [--check]

  protocol/inputs/*.json  ->  build_protocol_definition.py  ->  protocol/cozmo_robot_protocol.json
                          ->  gen_protocol.py               ->  cozmo-stack/src/Cozmo.Protocol/Generated/*.g.cs
                          ->  gen_protocol_status.py        ->  PROTOCOL_STATUS.md

With --check nothing is left modified: the outputs are rebuilt into a temporary tree and compared with what
is committed, so CI or a reviewer can confirm the generated files match their inputs.
"""
import argparse
import filecmp
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
RE_ANALYSIS = TOOLS.parent
ROOT = RE_ANALYSIS.parent
STACK = ROOT / "cozmo-stack"

OUTPUTS = [
    RE_ANALYSIS / "protocol" / "cozmo_robot_protocol.json",
    STACK / "src" / "Cozmo.Protocol" / "Generated" / "MessageCatalog.g.cs",
    STACK / "src" / "Cozmo.Protocol" / "Generated" / "RobotMessages.g.cs",
    RE_ANALYSIS / "PROTOCOL_STATUS.md",
]


def run(*args):
    r = subprocess.run([sys.executable, *[str(a) for a in args]], capture_output=True, text=True)
    if r.returncode != 0:
        sys.stderr.write(r.stdout + r.stderr)
        sys.exit(f"failed: {' '.join(str(a) for a in args)}")
    return r.stdout.strip()


def regenerate(re_analysis, stack):
    print(run(TOOLS / "build_protocol_definition.py", re_analysis / "protocol" / "inputs", re_analysis))
    print(run(TOOLS / "gen_protocol.py", re_analysis, stack))
    print(run(TOOLS / "gen_protocol_status.py", re_analysis))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="rebuild into a temporary tree and compare, leaving the working tree untouched")
    args = ap.parse_args()

    if not args.check:
        regenerate(RE_ANALYSIS, STACK)
        print("\nregenerated:")
        for o in OUTPUTS:
            print(f"  {o.relative_to(ROOT)}")
        return

    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        shutil.copytree(RE_ANALYSIS / "protocol", tmp / "re-analysis" / "protocol")
        shutil.copytree(RE_ANALYSIS / "captures", tmp / "re-analysis" / "captures")
        (tmp / "cozmo-stack" / "src" / "Cozmo.Protocol" / "Generated").mkdir(parents=True)
        regenerate(tmp / "re-analysis", tmp / "cozmo-stack")

        differing = []
        for o in OUTPUTS:
            rebuilt = tmp / o.relative_to(ROOT)
            if not rebuilt.exists() or not filecmp.cmp(o, rebuilt, shallow=False):
                differing.append(o.relative_to(ROOT))
        if differing:
            print("\nthese committed files do not match what their inputs produce:")
            for d in differing:
                print(f"  {d}")
            sys.exit(1)
        print("\nall generated files match their inputs")


if __name__ == "__main__":
    main()
