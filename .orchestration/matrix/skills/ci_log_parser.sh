#!/usr/bin/env bash
# Trace Log Analyzer: strips environment noise from a failed dotnet build/test
# or gh run log and extracts the exact failure blocks (error codes, stack
# traces, file:line targets) for the repair loop. Returns JSON on stdout.
#
# Usage:
#   ci_log_parser.sh <path-to-log-file>
#   ci_log_parser.sh -                 # read log from stdin
#   ci_log_parser.sh --gh-run <run_id> # fetch failed job log via gh CLI
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_lib.sh"
require_jq

if [[ "${1:-}" == "--gh-run" ]]; then
  run_id="${2:?run_id required}"
  command -v gh >/dev/null 2>&1 || die "gh CLI is required for --gh-run"
  log_content="$(gh run view "$run_id" --log-failed 2>/dev/null)" || die "failed to fetch log for run $run_id"
elif [[ "${1:-}" == "-" || -z "${1:-}" ]]; then
  log_content="$(cat)"
else
  [[ -f "$1" ]] || die "log file not found: $1"
  log_content="$(cat "$1")"
fi

# Portable across BSD grep (macOS) and GNU grep (CI): avoid --no-group-separator
# and \s (both GNU-only extensions), and strip the "--" context separator via jq instead.
echo "$log_content" | grep -E \
  -e 'error (CS|NETSDK|MSB)[0-9]+' \
  -e '^[[:space:]]*at .+ in .+:line [0-9]+' \
  -e 'Failed!.*Passed:.*Failed:' \
  -e '\[FAIL\]' \
  -e 'Assert\.' \
  -e 'Exception:' \
  -A2 -B1 2>/dev/null > /tmp/orch_ci_matches.$$ || true

matches_file="/tmp/orch_ci_matches.$$"
jq -Rsc '{failure_lines: (split("\n") | map(select(length > 0 and . != "--"))), failure_count: (split("\n") | map(select(length > 0 and . != "--")) | length)}' "$matches_file"
rm -f "$matches_file"
