#!/bin/sh
# Run the fidelity check and the full test suite on the committed HEAD, and record the pass for the push gate, so a
# later `git push` of the same code does not run the suite again. Use this instead of a bare `dotnet test` before
# pushing (operator, 2026-10-18).
root=$(git rev-parse --show-toplevel); cd "$root" || exit 1
. scripts/hooks/gate-key.sh
if ! git diff --quiet HEAD -- || [ -n "$(git ls-files --others --exclude-standard -- cozmo-stack)" ]; then
    echo "test-full: commit (or move) your changes under cozmo-stack first; only committed code is recorded."; exit 1
fi
mkdir -p .scratch; log=.scratch/test-full.log
python re-analysis/tools/fidelity.py --check > "$log" 2>&1 || { tail -20 "$log"; echo "test-full: fidelity.py --check failed."; exit 1; }
key=$(gate_key HEAD)
echo "test-full: full suite for $(git rev-parse --short HEAD); log in $log"
if ! dotnet test cozmo-stack/Cozmo.sln --blame-hang --blame-hang-timeout 5m >> "$log" 2>&1; then
    grep -E "\[FAIL\]|Failed!|error CS|hang|Blame|active test" "$log" | head -20; echo "test-full: FAILED."; exit 1
fi
grep -E "Passed!" "$log" | tail -1
echo "$key" >> .scratch/pre-push-passed
echo "test-full: passed and recorded; pushing this code skips the suite."
