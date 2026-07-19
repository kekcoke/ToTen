#!/usr/bin/env bash
# Implementation Loop skill: drive an agent to implement one task inside its
# provisioned worktree, then commit the result. This is the execution half the
# scaffold was missing — deterministic git/state plumbing here, code judgment
# delegated to a headless agent.
#
# The agent is invoked via ${ORCH_AGENT_CMD:-claude -p} so the loop is
# autonomous but not hard-wired to one CLI; the rendered prompt is piped on its
# stdin. Set ORCH_DRY_RUN=1 (or pass --dry-run) to print the prompt + planned
# actions without invoking the agent or committing.
#
# The task's agent contract (matrix/agents/<agent>.json) is enforced here: any
# changed file must match an allowed_paths glob, and touching an
# escalation_required_paths entry aborts + escalates instead of committing.
#
# Usage:
#   implementation_runner.sh <task_id> [--dry-run]
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_lib.sh"
require_jq
require_state_file
command -v python3 >/dev/null 2>&1 || die "python3 is required but not installed"

task_id="${1:?task_id required}"
[[ "${2:-}" == "--dry-run" ]] && ORCH_DRY_RUN=1
dry_run="${ORCH_DRY_RUN:-0}"

# ── Resolve task + worktree + agent contract ─────────────────────────────────
task="$(jq -c --arg id "$task_id" '.tasks[] | select(.id == $id)' "$STATE_FILE")"
[[ -n "$task" ]] || die "unknown task_id: $task_id"

path="$(jq -r --arg id "$task_id" '.worktrees[$id].path // empty' "$WORKTREES_FILE")"
branch="$(jq -r --arg id "$task_id" '.worktrees[$id].branch // empty' "$WORKTREES_FILE")"
# A real run needs a provisioned worktree; a dry-run only renders the prompt.
if [[ "${ORCH_DRY_RUN:-0}" != "1" && "${2:-}" != "--dry-run" ]]; then
  [[ -n "$path" && -d "$path" ]] || die "no provisioned worktree for $task_id — run orch-advance first"
fi

agent="$(echo "$task" | jq -r '.agent // empty')"
contract="$ORCH_DIR/matrix/agents/$agent.json"
[[ -f "$contract" ]] || die "agent contract not found: $contract"

title="$(echo "$task" | jq -r '.title')"
domain="$(echo "$task" | jq -r '.domain')"
source_ref="$(echo "$task" | jq -r '.source // empty')"
files_scope="$(echo "$task" | jq -r '.files_scope | join("\n  - ")')"
allowed="$(jq -r '.allowed_paths | join("\n  - ")' "$contract")"
escalation="$(jq -r '.escalation_required_paths | join("\n  - ")' "$contract")"

# ── Render the agent prompt ──────────────────────────────────────────────────
prompt="$(cat <<PROMPT
You are the '$agent' agent implementing a single scoped task in this git worktree.

TASK: $title
DOMAIN: $domain
SOURCE (read this for the full requirement): $source_ref

You MAY only create/edit files matching these allowed paths:
  - $allowed

You MUST NOT touch these paths (they require human escalation):
  - $escalation

FILES IN SCOPE for this task:
  - $files_scope

Implement the task completely and idiomatically, matching the surrounding code
style. Add or update the corresponding integration tests. Do not commit — the
orchestrator commits your result. When done, stop.
PROMPT
)"

if [[ "$dry_run" == "1" ]]; then
  log_info "[dry-run] would run agent for $task_id in $path"
  jq -n --arg id "$task_id" --arg path "$path" --arg agent "$agent" \
        --arg cmd "${ORCH_AGENT_CMD:-claude -p}" --arg prompt "$prompt" \
    '{status: "dry_run", task_id: $id, worktree: $path, agent: $agent, agent_cmd: $cmd, prompt: $prompt}'
  exit 0
fi

# ── Invoke the agent inside the worktree ─────────────────────────────────────
agent_cmd="${ORCH_AGENT_CMD:-claude -p}"
log_info "invoking agent for $task_id: $agent_cmd (cwd=$path)"
if ! ( cd "$path" && printf '%s\n' "$prompt" | eval "$agent_cmd" >&2 ); then
  die "agent command failed for $task_id: $agent_cmd"
fi

# ── Enforce the contract against the produced diff ───────────────────────────
changed="$(git -C "$path" status --porcelain | sed 's/^...//')"
if [[ -z "$changed" ]]; then
  die "agent produced no changes for $task_id — nothing to commit"
fi

allowed_json="$(jq -c '.allowed_paths' "$contract")"
escalation_json="$(jq -c '.escalation_required_paths' "$contract")"
# The changed-file list travels via the CHANGED env var, not stdin — stdin here
# is the heredoc feeding the program to `python3 -`, so the two would collide.
violation="$(CHANGED="$changed" python3 - "$allowed_json" "$escalation_json" <<'PYEOF'
import json, os, sys, fnmatch
allowed = json.loads(sys.argv[1])
escalation = json.loads(sys.argv[2])
changed = [l.strip() for l in os.environ.get("CHANGED", "").splitlines() if l.strip()]
def matches(path, globs):
    return any(fnmatch.fnmatch(path, g) for g in globs)
out_of_scope, escalated = [], []
for f in changed:
    if matches(f, escalation):
        escalated.append(f)
    elif not matches(f, allowed):
        out_of_scope.append(f)
print(json.dumps({"out_of_scope": out_of_scope, "escalated": escalated}))
PYEOF
)"

escalated="$(echo "$violation" | jq -c '.escalated')"
out_of_scope="$(echo "$violation" | jq -c '.out_of_scope')"

if [[ "$(echo "$escalated" | jq 'length')" -gt 0 || "$(echo "$out_of_scope" | jq 'length')" -gt 0 ]]; then
  # Revert the worktree and block the task rather than committing a contract breach.
  git -C "$path" reset --hard HEAD >&2
  git -C "$path" clean -fd >&2
  now="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  reason="agent '$agent' violated contract: escalated=$(echo "$escalated" | jq -c .) out_of_scope=$(echo "$out_of_scope" | jq -c .)"
  tmp="$(mktemp)"
  jq --arg id "$task_id" --arg now "$now" --arg reason "$reason" '
    (.tasks[] | select(.id == $id) | .status) = "blocked"
    | .escalations += [{task_id: $id, reason: $reason, flagged_at: $now}]
    | .updated_at = $now
  ' "$STATE_FILE" > "$tmp" && mv "$tmp" "$STATE_FILE"
  log_error "$task_id BLOCKED — contract violation, worktree reverted"
  jq -n --arg id "$task_id" --argjson esc "$escalated" --argjson oos "$out_of_scope" \
    '{status: "BLOCKED", task_id: $id, escalated: $esc, out_of_scope: $oos}'
  exit 1
fi

# ── Commit the vetted change ─────────────────────────────────────────────────
git -C "$path" add -A >&2
git -C "$path" commit -m "feat($domain): $title [orch:$task_id]" >&2
commit="$(git -C "$path" rev-parse HEAD)"

log_ok "$task_id implemented and committed ($commit)"
jq -n --arg id "$task_id" --arg commit "$commit" --arg branch "$branch" \
  '{status: "implemented", task_id: $id, commit: $commit, branch: $branch}'
