#!/usr/bin/env bash
# promote_to_engineering: once a roadmap item's decision doc records a chosen
# option (a "**Decision criteria (resolved):**" line under its section),
# appends a real task into the *existing* engineering ../../state.json using
# its exact schema, so Part 1's orch-advance/dependency_grapher/escalation
# gates pick it up transparently — no changes needed on that side.
#
# Usage: promote_to_engineering.sh <item_id> <agent_name> [strategy]
#   agent_name must be one of the existing matrix/agents/*.json roles
#   strategy defaults to "parallel"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_lib.sh"
require_jq
require_roadmap_state_file
require_state_file

item_id="${1:?item_id required}"
agent_name="${2:?agent_name required (e.g. marketplace-agent)}"
strategy="${3:-parallel}"

item="$(jq -c --arg id "$item_id" '.items[] | select(.id == $id)' "$ROADMAP_STATE_FILE")"
[[ -n "$item" ]] || die "unknown roadmap item_id: $item_id"

already="$(echo "$item" | jq -r '.promotes_to_task // empty')"
[[ -z "$already" ]] || die "$item_id was already promoted to task: $already"

decision_doc_path="$(echo "$item" | jq -r '.decision_doc_path // empty')"
[[ -n "$decision_doc_path" ]] || die "$item_id has no decision doc yet — run scoping_doc_writer.sh first"

doc_rel="${decision_doc_path%%#*}"
doc_file="$REPO_ROOT/$doc_rel"
[[ -f "$doc_file" ]] || die "decision doc not found: $doc_file"

decision_line="$(grep -E '^\*\*Decision criteria \(resolved\):\*\*' "$doc_file" | tail -1 || true)"
[[ -n "$decision_line" ]] || die "no '**Decision criteria (resolved):**' line found in $doc_rel yet — a human must record the decision before promotion"

agent_file="$ORCH_DIR/matrix/agents/${agent_name}.json"
[[ -f "$agent_file" ]] || die "unknown engineering agent: $agent_name (no $agent_file)"

domain="$(echo "$item" | jq -r '.domain')"
files_scope="$(python3 -c "
import json, sys
domain = sys.argv[1]
parts = [d.strip() for d in domain.split('/')]
print(json.dumps([f'src/ToTen.Api/Features/{p}/**' for p in parts]))
" "$domain")"

new_task="$(jq -cn \
  --arg id "$item_id" \
  --arg title "$(echo "$item" | jq -r '.title')" \
  --arg domain "$domain" \
  --arg strategy "$strategy" \
  --arg agent "$agent_name" \
  --argjson files_scope "$files_scope" \
  --arg source "$decision_doc_path" \
  '{
    id: $id, title: $title, domain: $domain, status: "queued",
    strategy: $strategy, agent: $agent, files_scope: $files_scope,
    depends_on: [], soft_conflicts_with: [], source: $source
  }')"

existing_task="$(jq -c --arg id "$item_id" '.tasks[] | select(.id == $id)' "$STATE_FILE")"
[[ -z "$existing_task" ]] || die "a task with id $item_id already exists in $STATE_FILE"

now="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
tmp="$(mktemp)"
jq --argjson task "$new_task" --arg now "$now" '.tasks += [$task] | .updated_at = $now' "$STATE_FILE" > "$tmp" && mv "$tmp" "$STATE_FILE"

tmp2="$(mktemp)"
jq --arg id "$item_id" --arg now "$now" '
  (.items[] | select(.id == $id) | .status) = "promoted"
  | (.items[] | select(.id == $id) | .promotes_to_task) = $id
  | .updated_at = $now
' "$ROADMAP_STATE_FILE" > "$tmp2" && mv "$tmp2" "$ROADMAP_STATE_FILE"

log_ok "promoted $item_id into engineering state.json as task $item_id (agent: $agent_name)"
jq -cn --arg id "$item_id" --arg decision "$decision_line" '{item_id: $id, promoted_task_id: $id, decision: $decision}'
