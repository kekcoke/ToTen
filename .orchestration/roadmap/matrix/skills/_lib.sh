#!/usr/bin/env bash
# Shared helpers for the roadmap (PRM) matrix. Reuses the engineering loop's
# _lib.sh for REPO_ROOT/ORCH_DIR/logging, then adds roadmap-specific paths.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/../../../matrix/skills/_lib.sh"

ROADMAP_DIR="$ORCH_DIR/roadmap"
ROADMAP_STATE_FILE="$ROADMAP_DIR/roadmap-state.json"
ENGINEERING_STATE_FILE="$STATE_FILE"

require_roadmap_state_file() {
  [[ -f "$ROADMAP_STATE_FILE" ]] || die "$ROADMAP_STATE_FILE not found — run prm-init first"
}
