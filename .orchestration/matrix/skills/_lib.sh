#!/usr/bin/env bash
# Shared helpers sourced by every matrix/commands and matrix/skills script.
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
ORCH_DIR="$REPO_ROOT/.orchestration"
STATE_FILE="$ORCH_DIR/state.json"
WORKTREES_FILE="$ORCH_DIR/active-worktrees.json"
WORKTREES_DIR="$ORCH_DIR/worktrees"

if [[ -t 2 ]]; then
  C_RED=$'\033[31m'; C_GREEN=$'\033[32m'; C_YELLOW=$'\033[33m'; C_RESET=$'\033[0m'
else
  C_RED=""; C_GREEN=""; C_YELLOW=""; C_RESET=""
fi

log_info()  { echo "${C_YELLOW}[orch]${C_RESET} $*" >&2; }
log_ok()    { echo "${C_GREEN}[orch]${C_RESET} $*" >&2; }
log_error() { echo "${C_RED}[orch]${C_RESET} $*" >&2; }
die()       { log_error "$*"; exit 1; }

require_state_file() {
  [[ -f "$STATE_FILE" ]] || die "$STATE_FILE not found — run orch-init first"
}

require_jq() {
  command -v jq >/dev/null 2>&1 || die "jq is required but not installed"
}
