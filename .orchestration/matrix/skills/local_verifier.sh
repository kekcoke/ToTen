#!/usr/bin/env bash
# Local Verification Loop: mirrors the `lint-test` job in
# .github/workflows/azure-dev.yml exactly (restore, build, both test
# projects) so a failure here reliably predicts a failure on the PR's only
# pre-merge CI gate. Returns JSON on stdout; non-zero exit on failure.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_lib.sh"
require_jq

cd "$REPO_ROOT"
mkdir -p "$ORCH_DIR/logs"
log_file="$ORCH_DIR/logs/local_verify_$(date -u +%Y%m%dT%H%M%SZ).log"

status="passed"
step="restore"
{
  dotnet restore &&
  { step="build"; dotnet build --configuration Release --no-restore; } &&
  { step="test-api"; dotnet test tests/ToTen.Api.IntegrationTests/ToTen.Api.IntegrationTests.csproj \
      --configuration Release --no-build --logger "trx;LogFileName=test-results.trx"; } &&
  { step="test-worker"; dotnet test tests/ToTen.Worker.Tests/ToTen.Worker.Tests.csproj \
      --configuration Release --no-build --logger "trx;LogFileName=worker-test-results.trx"; }
} > "$log_file" 2>&1 || status="failed"

if [[ "$status" == "failed" ]]; then
  log_error "local verification failed at step: $step (see $log_file for full output)"
  jq -n --arg step "$step" --arg log "$log_file" \
    '{status: "failed", failed_step: $step, log_path: $log}'
  exit 1
fi

rm -f "$log_file"
log_ok "local verification passed (restore, build, both test projects)"
jq -n '{status: "passed"}'
