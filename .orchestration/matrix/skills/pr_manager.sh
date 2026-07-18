#!/usr/bin/env bash
# Remote Merge Request skill: push a task's branch, open its PR, and (once
# checks are green) squash-merge it. PR numbers are recorded on the task in
# state.json so the poller/complete steps can find them. Returns JSON on stdout.
#
# Usage:
#   pr_manager.sh push  <task_id>   # git push -u origin <branch>
#   pr_manager.sh open  <task_id>   # gh pr create (idempotent) -> stores pr_number
#   pr_manager.sh merge <task_id>   # squash-merge if watched checks are green
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_lib.sh"
require_jq
require_state_file

cmd="${1:-}"; task_id="${2:?task_id required}"

task="$(jq -c --arg id "$task_id" '.tasks[] | select(.id == $id)' "$STATE_FILE")"
[[ -n "$task" ]] || die "unknown task_id: $task_id"

path="$(jq -r --arg id "$task_id" '.worktrees[$id].path // empty' "$WORKTREES_FILE")"
branch="$(jq -r --arg id "$task_id" '.worktrees[$id].branch // empty' "$WORKTREES_FILE")"
title="$(echo "$task" | jq -r '.title')"
source_ref="$(echo "$task" | jq -r '.source // empty')"

store_pr() {
  local pr="$1" now
  now="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  local tmp; tmp="$(mktemp)"
  jq --arg id "$task_id" --arg now "$now" --argjson pr "$pr" \
    '(.tasks[] | select(.id == $id) | .pr_number) = $pr | .updated_at = $now' \
    "$STATE_FILE" > "$tmp" && mv "$tmp" "$STATE_FILE"
}

case "$cmd" in
  push)
    [[ -n "$branch" && -n "$path" ]] || die "no worktree/branch for $task_id"
    git remote get-url origin >/dev/null 2>&1 || die "no 'origin' remote configured"
    git -C "$path" push -u origin "$branch" >&2
    log_ok "pushed $branch"
    jq -n --arg id "$task_id" --arg branch "$branch" '{status: "pushed", task_id: $id, branch: $branch}'
    ;;

  open)
    command -v gh >/dev/null 2>&1 || die "gh CLI is required"
    existing="$(echo "$task" | jq -r '.pr_number // empty')"
    if [[ -n "$existing" ]]; then
      log_info "PR #$existing already recorded for $task_id"
      jq -n --arg id "$task_id" --argjson pr "$existing" '{status: "exists", task_id: $id, pr_number: $pr}'
      exit 0
    fi
    body="Automated implementation of orchestration task \`$task_id\`.

Source: $source_ref

Opened by the orchestration loop (pr_manager.sh)."
    url="$(gh pr create --base main --head "$branch" --title "$title" --body "$body" 2>&1)" \
      || die "gh pr create failed: $url"
    pr_number="$(echo "$url" | grep -oE '[0-9]+$' | tail -1)"
    [[ -n "$pr_number" ]] || die "could not parse PR number from: $url"
    store_pr "$pr_number"
    log_ok "opened PR #$pr_number for $task_id"
    jq -n --arg id "$task_id" --argjson pr "$pr_number" --arg url "$url" \
      '{status: "opened", task_id: $id, pr_number: $pr, url: $url}'
    ;;

  merge)
    command -v gh >/dev/null 2>&1 || die "gh CLI is required"
    pr_number="$(echo "$task" | jq -r '.pr_number // empty')"
    [[ -n "$pr_number" ]] || die "no pr_number recorded for $task_id — run 'open' first"

    # Guard: only merge when every watched check is green (mirrors remote_ci_poller).
    watched="$(jq -c '.verification.remote.watched_checks' "$STATE_FILE")"
    green="$(gh pr checks "$pr_number" --json name,state 2>/dev/null | jq -c --argjson watched "$watched" '
      [.[] | select(.name as $n | $watched | index($n) != null)]
      | (length > 0) and all(.[]; .state == "SUCCESS")')"
    [[ "$green" == "true" ]] || die "watched checks not all green on PR #$pr_number — refusing to merge"

    gh pr merge "$pr_number" --squash --delete-branch >&2
    log_ok "merged PR #$pr_number for $task_id"
    jq -n --arg id "$task_id" --argjson pr "$pr_number" '{status: "merged", task_id: $id, pr_number: $pr}'
    ;;

  *)
    die "usage: pr_manager.sh {push|open|merge} <task_id>"
    ;;
esac
