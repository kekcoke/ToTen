#!/usr/bin/env bash
# footnote_scanner: parses ABOUT.md's Footnotes section and flags footnotes
# that are (a) not marked resolved in their own text, and (b) not already
# cross-referenced/tracked in roadmap-state.json (items, backlog_stubs, or
# cross_referenced_footnotes). Returns JSON on stdout.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_lib.sh"
require_jq
require_roadmap_state_file

about_file="$REPO_ROOT/ABOUT.md"
[[ -f "$about_file" ]] || die "ABOUT.md not found at repo root"

# Extract "[^N]: text" footnote lines. Portable ERE (no \s / GNU-only extensions).
footnote_lines="$(grep -E '^\[\^[0-9]+\]:' "$about_file" || true)"
[[ -n "$footnote_lines" ]] || die "no footnotes found in ABOUT.md — has the format changed?"

tracked="$(jq -c '
  (.items // [] | map(.footnotes[]?)) +
  (.backlog_stubs // [] | map(.footnotes[]?)) +
  (.cross_referenced_footnotes // {} | keys)
' "$ROADMAP_STATE_FILE")"

python3 - "$tracked" "$footnote_lines" <<'PYEOF'
import json, re, sys

tracked = set(json.loads(sys.argv[1]))
lines = sys.argv[2].splitlines()

resolved_pattern = re.compile(r'^\[\^(\d+)\]:\s*\*\*(Resolved|Stale — already resolved)', re.IGNORECASE)
footnote_pattern = re.compile(r'^\[\^(\d+)\]:\s*(.*)$')

gaps = []
resolved = []
for line in lines:
    m = footnote_pattern.match(line)
    if not m:
        continue
    num, text = m.group(1), m.group(2)
    key = f"ABOUT.md#^{num}"
    if resolved_pattern.match(line):
        resolved.append(key)
        continue
    if key in tracked:
        continue
    gaps.append({"footnote": key, "text": text.strip()})

print(json.dumps({"untracked_gaps": gaps, "resolved": resolved, "already_tracked_count": len(tracked)}))
PYEOF
