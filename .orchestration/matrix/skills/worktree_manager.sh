#!/usr/bin/env bash
# Worktree Architect skill: isolate parallel tasks via git worktree, track them
# in active-worktrees.json, and clean up on merge. Returns JSON on stdout.
#
# Usage:
#   worktree_manager.sh add <task_id> <branch> [<base_ref>]
#   worktree_manager.sh remove <task_id> [--delete-branch-if-merged]
#   worktree_manager.sh list
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_lib.sh"
require_jq

cmd="${1:-}"; shift || true

case "$cmd" in
  add)
    task_id="${1:?task_id required}"; branch="${2:?branch required}"; base_ref="${3:-main}"
    mkdir -p "$WORKTREES_DIR"
    path="$WORKTREES_DIR/$task_id"
    [[ -e "$path" ]] && die "worktree path already exists: $path"

    if git show-ref --verify --quiet "refs/heads/$branch"; then
      git worktree add "$path" "$branch" >&2
    else
      git worktree add -b "$branch" "$path" "$base_ref" >&2
    fi

    [[ -f "$WORKTREES_FILE" ]] || echo '{"version":1,"worktrees":{}}' > "$WORKTREES_FILE"
    tmp="$(mktemp)"
    jq --arg id "$task_id" --arg path "$path" --arg branch "$branch" --arg base "$base_ref" \
       --arg now "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
       '.worktrees[$id] = {path: $path, branch: $branch, base_ref: $base, created_at: $now} | .updated_at = $now' \
       "$WORKTREES_FILE" > "$tmp" && mv "$tmp" "$WORKTREES_FILE"

    jq -n --arg id "$task_id" --arg path "$path" --arg branch "$branch" \
      '{task_id: $id, path: $path, branch: $branch, status: "created"}'
    ;;

  remove)
    task_id="${1:?task_id required}"; delete_flag="${2:-}"
    require_state_file
    entry="$(jq -r --arg id "$task_id" '.worktrees[$id] // empty' "$WORKTREES_FILE")"
    [[ -n "$entry" ]] || die "no tracked worktree for task_id: $task_id"

    path="$(echo "$entry" | jq -r '.path')"
    branch="$(echo "$entry" | jq -r '.branch')"

    git worktree remove "$path" >&2 2>/dev/null || git worktree remove --force "$path" >&2
    git worktree prune >&2

    branch_deleted=false
    if [[ "$delete_flag" == "--delete-branch-if-merged" ]]; then
      if git merge-base --is-ancestor "$branch" main 2>/dev/null; then
        git branch -d "$branch" >&2
        branch_deleted=true
      else
        log_info "branch $branch not fully merged into main — leaving it in place"
      fi
    fi

    tmp="$(mktemp)"
    jq --arg id "$task_id" --arg now "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
       'del(.worktrees[$id]) | .updated_at = $now' "$WORKTREES_FILE" > "$tmp" && mv "$tmp" "$WORKTREES_FILE"

    jq -n --arg id "$task_id" --argjson deleted "$branch_deleted" \
      '{task_id: $id, status: "removed", branch_deleted: $deleted}'
    ;;

  list)
    [[ -f "$WORKTREES_FILE" ]] || echo '{"version":1,"worktrees":{}}'
    [[ -f "$WORKTREES_FILE" ]] && jq -c '.worktrees' "$WORKTREES_FILE"
    ;;

  *)
    die "usage: worktree_manager.sh {add|remove|list} ..."
    ;;
esac
