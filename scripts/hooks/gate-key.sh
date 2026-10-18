# Shared by pre-push and scripts/test-full.sh: the fingerprint of everything the full test suite's result depends on
# (the code and the fidelity records) at commit $1. Same fingerprint, same result.
gate_key() {
    for p in cozmo-stack re-analysis/fidelity_manifest.json re-analysis/FIDELITY_GAPS.md re-analysis/inventory re-analysis/tools; do
        git rev-parse -q --verify "$1:$p" || echo "none:$p"
    done | git hash-object --stdin
}
