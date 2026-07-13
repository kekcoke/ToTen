#!/usr/bin/env bash
# scoping_doc_writer: appends a new decision-record section to
# docs/section-2-flagged-issues.md, in the same shape as its existing
# entries (problem statement pulled from the item's ABOUT.md footnotes,
# options table, decision criteria) for a roadmap item. This is a mechanical
# scaffold, not content authorship — the options/decision-criteria rows are
# left as clearly marked TODOs for the roadmap-scoping-agent (or a human) to
# fill in with actual product judgment before a decision can be recorded.
#
# Usage: scoping_doc_writer.sh <item_id>
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_lib.sh"
require_jq
require_roadmap_state_file

item_id="${1:?item_id required}"
doc_file="$REPO_ROOT/docs/section-2-flagged-issues.md"
about_file="$REPO_ROOT/ABOUT.md"
[[ -f "$doc_file" ]] || die "$doc_file not found"

item="$(jq -c --arg id "$item_id" '.items[] | select(.id == $id)' "$ROADMAP_STATE_FILE")"
[[ -n "$item" ]] || die "unknown roadmap item_id (or it's a backlog_stub, which isn't scaffolded): $item_id"

existing_doc="$(echo "$item" | jq -r '.decision_doc_path // empty')"
if [[ -n "$existing_doc" ]]; then
  log_info "decision doc already exists for $item_id at $existing_doc — no-op"
  jq -cn --arg id "$item_id" --arg path "$existing_doc" '{item_id: $id, status: "unchanged", decision_doc_path: $path}'
  exit 0
fi

title="$(echo "$item" | jq -r '.title')"
footnotes="$(echo "$item" | jq -r '.footnotes[]')"

problem_text=""
while IFS= read -r fn; do
  num="${fn##*\^}"
  line="$(grep -E "^\[\^${num}\]:" "$about_file" || true)"
  [[ -n "$line" ]] && problem_text+="- ${line}"$'\n'
done <<< "$footnotes"

anchor_title="$(echo "$title" | tr '[:upper:]' '[:lower:]' | tr -c '[:alnum:]' '-' | sed 's/-\+/-/g; s/^-//; s/-$//')"
anchor="roadmap---${anchor_title}"

{
  echo ""
  echo "---"
  echo ""
  echo "## Roadmap — ${title}"
  echo ""
  echo "**Status:** New, flagged from ABOUT.md footnote fact-check — not resolved. No code change in this pass."
  echo ""
  echo "**Source:** roadmap item \`${item_id}\` (\`.orchestration/roadmap/roadmap-state.json\`), footnotes: $(echo "$item" | jq -r '.footnotes | join(", ")')"
  echo ""
  echo "**The problem (from ABOUT.md's fact-check):**"
  echo ""
  printf '%s' "$problem_text"
  echo ""
  echo "| Option | Pros | Cons |"
  echo "|---|---|---|"
  echo "| <!-- TODO: option 1 --> | <!-- TODO --> | <!-- TODO --> |"
  echo "| <!-- TODO: option 2 --> | <!-- TODO --> | <!-- TODO --> |"
  echo ""
  echo "**Decision criteria:** <!-- TODO: product owner / roadmap-scoping-agent to fill in, then add a line starting"
  echo "\"**Decision criteria (resolved):**\" once an option is chosen — that line is what \`prm-promote\` looks for. -->"
} >> "$doc_file"

now="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
doc_path="docs/section-2-flagged-issues.md#${anchor}"
tmp="$(mktemp)"
jq --arg id "$item_id" --arg path "$doc_path" --arg now "$now" '
  (.items[] | select(.id == $id) | .status) = "needs_decision"
  | (.items[] | select(.id == $id) | .decision_doc_path) = $path
  | .updated_at = $now
' "$ROADMAP_STATE_FILE" > "$tmp" && mv "$tmp" "$ROADMAP_STATE_FILE"

log_ok "scaffolded decision-record section for $item_id at $doc_path"
jq -cn --arg id "$item_id" --arg path "$doc_path" '{item_id: $id, status: "scaffolded", decision_doc_path: $path}'
