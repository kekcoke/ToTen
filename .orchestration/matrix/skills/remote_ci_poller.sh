#!/usr/bin/env bash
# Remote Merge Request CI skill: polls only the checks that actually gate a
# PR pre-merge (per azure-dev.yml's dependency graph, that's `lint-test`, plus
# the independent `analyze` job from codeql.yml — everything else in
# azure-dev.yml is main-only/post-deploy and irrelevant to a merge decision).
# Watched check names come from state.json's verification.remote.watched_checks.
#
# Usage:
#   remote_ci_poller.sh <pr-number>          # one-shot snapshot
#   remote_ci_poller.sh <pr-number> --wait   # poll every 30s for up to 20m
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_lib.sh"
require_jq
require_state_file
command -v gh >/dev/null 2>&1 || die "gh CLI is required"

pr_number="${1:?pr-number required}"
mode="${2:-}"

watched="$(jq -c '.verification.remote.watched_checks' "$STATE_FILE")"

snapshot() {
  gh pr checks "$pr_number" --json name,state,link 2>/dev/null | \
    jq -c --argjson watched "$watched" '
      [.[] | select(.name as $n | $watched | index($n) != null)]
      | { checks: ., all_concluded: (all(.[]; .state == "SUCCESS" or .state == "FAILURE") // true),
          all_passed: (all(.[]; .state == "SUCCESS") // false) }'
}

if [[ "$mode" != "--wait" ]]; then
  snapshot
  exit 0
fi

deadline=$(( $(date +%s) + 1200 ))
while (( $(date +%s) < deadline )); do
  result="$(snapshot)"
  concluded="$(echo "$result" | jq -r '.all_concluded')"
  if [[ "$concluded" == "true" ]]; then
    echo "$result"
    passed="$(echo "$result" | jq -r '.all_passed')"
    [[ "$passed" == "true" ]] && exit 0 || exit 1
  fi
  log_info "watched checks still pending on PR #$pr_number, retrying in 30s"
  sleep 30
done

die "timed out after 20m waiting on PR #$pr_number checks"
