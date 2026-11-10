#!/bin/sh
# Run the fidelity check and the full test suite on the committed HEAD, and record the pass for the push gate, so a
# later `git push` of the same code does not run the suite again. Use this instead of a bare `dotnet test` before
# pushing (operator, 2026-10-18).
root=$(git rev-parse --show-toplevel); cd "$root" || exit 1
. scripts/hooks/gate-key.sh
# Stop the compiler and MSBuild servers on exit: they stay resident after a build and grow by several GB over
# repeated runs (operator, 2026-10-27).
trap 'dotnet build-server shutdown >/dev/null 2>&1' EXIT
# A run whose shell was killed leaves dotnet test and its testhost running, and the trap above never fired: stop that run and
# the build servers first, so two suites never stack (operator, 2026-11-07).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/stop-stale-tests.ps1 -Root "$(cygpath -w "$root")" 2>/dev/null
dotnet build-server shutdown >/dev/null 2>&1
if ! git diff --quiet HEAD -- || [ -n "$(git ls-files --others --exclude-standard -- cozmo-stack)" ]; then
    echo "test-full: commit (or move) your changes under cozmo-stack first; only committed code is recorded."; exit 1
fi
mkdir -p .scratch; log=.scratch/test-full.log
python re-analysis/tools/fidelity.py --check > "$log" 2>&1 || { tail -20 "$log"; echo "test-full: fidelity.py --check failed."; exit 1; }
key=$(gate_key HEAD)
echo "test-full: full suite for $(git rev-parse --short HEAD); log in $log"
# Sample memory during the build and the tests, and log each test's completion time, so a low-memory kill names the tests that were
# running (operator, 2026-11-09). The sampler stops with the script.
mem=.scratch/test-mem.log; : > "$mem"; rm -f "$mem.stop"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/sample-test-memory.ps1 -Out "$(cygpath -w "$root")\.scratch\test-mem.log" -Log "$(cygpath -w "$root")\.scratch\test-full.log" &
sampler=$!
trap 'touch "$mem.stop"; kill $sampler 2>/dev/null; dotnet build-server shutdown >/dev/null 2>&1' EXIT
# Build first and stop the compiler servers before the tests: VBCSCompiler held 4.2 GB through a whole run and the
# suite was killed for low memory twice (operator, 2026-11-08).
# Two build nodes, not one per core: the 2026-11-09 run's low point (2.4 GB free) was the build, at 4.4 GB for
# dotnet/MSBuild/VBCSCompiler, not the tests (1.1 GB peak).
# No shared compiler server and no node reuse: the 2026-11-09 23:47 run was killed in the build with VBCSCompiler
# and two nodes at 3.6 GB; each compile now runs in its own csc that exits with its project.
# Workstation GC with memory conservation for the build processes: three runs on 2026-11-10 still peaked at
# 3.6-4.2 GB in the build with two nodes and no compiler server.
if ! DOTNET_gcServer=0 DOTNET_GCConserveMemory=7 dotnet build cozmo-stack/Cozmo.sln -maxcpucount:2 -nodeReuse:false -p:UseSharedCompilation=false >> "$log" 2>&1; then
    grep -E "error CS|error MSB" "$log" | head -20; echo "test-full: build FAILED."; exit 1
fi
dotnet build-server shutdown >/dev/null 2>&1
if ! dotnet test cozmo-stack/Cozmo.sln --no-build --blame-hang --blame-hang-timeout 5m --logger "console;verbosity=normal" >> "$log" 2>&1; then
    grep -E "\[FAIL\]|Failed!|error CS|hang|Blame|active test" "$log" | head -20; echo "test-full: FAILED."; exit 1
fi
grep -E "^ *(Total tests|Passed|Failed):" "$log" | tail -3 | tr -s ' ' | tr '\n' ' '; echo
awk '{for(i=2;i<=NF;i++){split($i,a,"=");if(a[1]=="free"&&(m==""||a[2]+0<m))m=a[2]+0;if(a[1]=="testhost"&&a[2]+0>t)t=a[2]+0}} END{print "test-full: memory: lowest free " m " MB, testhost peak " t " MB (" FILENAME ")"}' "$mem"
echo "$key" >> .scratch/pre-push-passed
echo "test-full: passed and recorded; pushing this code skips the suite."
