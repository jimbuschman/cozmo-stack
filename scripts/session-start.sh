#!/bin/sh
# Claude Code SessionStart hook (.claude/settings.json): orient a new session cheaply.
# Fast-forwards to origin/main when the tree allows it, then prints the head of PROJECT_STATE's current section and
# any queue holds. Its output becomes session context, so keep it short.
cd "$(git rev-parse --show-toplevel 2>/dev/null)" || exit 0

if git pull --ff-only -q 2>/dev/null; then
    echo "session-start: up to date with origin/main at $(git rev-parse --short HEAD)."
else
    echo "session-start: git pull --ff-only did not apply (local changes or diverged); check git status before working."
fi

echo
echo "== PROJECT_STATE.md, newest entries (read the file for more) =="
awk '/^## Where things stand/{on=1; next} on && /^## /{exit} on' PROJECT_STATE.md | grep -v '^[[:space:]]*$' | head -12 | cut -c1-240

echo
echo "== Queue holds (re-analysis/jobs/QUEUE.md) =="
grep -n '^\*\*HOLD' re-analysis/jobs/QUEUE.md | cut -c1-200 || true
open=$(grep -c '| open' re-analysis/jobs/QUEUE.md)
echo "rows marked open: $open"

echo
echo "Tools: python re-analysis/tools/known.py <address|name> shows what is already known before tracing anything."
