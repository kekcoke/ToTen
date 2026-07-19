#!/usr/bin/env bash
# plane_sync: push/pull roadmap items against a Plane.so project.
#
# Per developers.plane.so/api-reference (fetched during design):
#   - Auth header: X-API-Key: <api_key>
#   - Create: POST  {base_url}/api/v1/workspaces/{slug}/projects/{project_id}/<segment>/
#   - Update: PATCH {base_url}/api/v1/workspaces/{slug}/projects/{project_id}/<segment>/{issue_id}/
#   - Get:    GET   {base_url}/api/v1/workspaces/{slug}/projects/{project_id}/<segment>/{issue_id}/
# Plane is mid-migration from "issues" to "work-items" in that URL segment
# (docs conflict on the exact cutover date) — <segment> is read from
# roadmap-state.json's plane.issues_segment_env (default "issues"), so a 404
# is a one-line env-var fix, not a code change.
#
# Credentials (PLANE_API_KEY / PLANE_WORKSPACE_SLUG / PLANE_PROJECT_ID, or
# whatever env var names roadmap-state.json's plane.*_env fields point to)
# are never stored in the repo — this fails loudly if they're unset rather
# than silently no-op'ing.
#
# Usage:
#   plane_sync.sh push <item_id>
#   plane_sync.sh pull <item_id>
#   plane_sync.sh pull-all
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_lib.sh"
require_jq
require_roadmap_state_file
command -v curl >/dev/null 2>&1 || die "curl is required"

plane_cfg="$(jq -c '.plane' "$ROADMAP_STATE_FILE")"
enabled="$(echo "$plane_cfg" | jq -r '.enabled')"
[[ "$enabled" == "true" ]] || die "plane sync is disabled (roadmap-state.json .plane.enabled is false)"

base_url="$(echo "$plane_cfg" | jq -r '.base_url')"
api_key_env="$(echo "$plane_cfg" | jq -r '.api_key_env')"
workspace_slug_env="$(echo "$plane_cfg" | jq -r '.workspace_slug_env')"
project_id_env="$(echo "$plane_cfg" | jq -r '.project_id_env')"
segment_env="$(echo "$plane_cfg" | jq -r '.issues_segment_env')"
segment_default="$(echo "$plane_cfg" | jq -r '.issues_segment_default')"

api_key="${!api_key_env:-}"
workspace_slug="${!workspace_slug_env:-}"
project_id="${!project_id_env:-}"
segment="${!segment_env:-$segment_default}"

[[ -n "$api_key" ]] || die "\$$api_key_env is not set — cannot authenticate to Plane"
[[ -n "$workspace_slug" ]] || die "\$$workspace_slug_env is not set — cannot resolve Plane workspace"
[[ -n "$project_id" ]] || die "\$$project_id_env is not set — cannot resolve Plane project"

issues_url="${base_url}/api/v1/workspaces/${workspace_slug}/projects/${project_id}/${segment}"

curl_json() {
  # curl_json <method> <url> [json_body]
  local method="$1" url="$2" body="${3:-}"
  local args=(-sS -X "$method" -H "X-API-Key: $api_key" -H "Content-Type: application/json")
  [[ -n "$body" ]] && args+=(-d "$body")
  curl "${args[@]}" "$url"
}

cmd="${1:?usage: plane_sync.sh {push|pull|pull-all} [item_id]}"
item_id="${2:-}"

case "$cmd" in
  push)
    [[ -n "$item_id" ]] || die "item_id required for push"
    item="$(jq -c --arg id "$item_id" '.items[] | select(.id == $id)' "$ROADMAP_STATE_FILE")"
    [[ -n "$item" ]] || die "unknown roadmap item_id: $item_id"

    name="$(echo "$item" | jq -r '.title')"
    existing_issue_id="$(echo "$item" | jq -r '.plane_issue_id // empty')"
    body="$(jq -cn --arg name "$name" '{name: $name, priority: "medium"}')"

    if [[ -n "$existing_issue_id" ]]; then
      response="$(curl_json PATCH "${issues_url}/${existing_issue_id}/" "$body")"
      issue_id="$existing_issue_id"
    else
      response="$(curl_json POST "${issues_url}/" "$body")"
      issue_id="$(echo "$response" | jq -r '.id // empty')"
    fi

    [[ -n "$issue_id" ]] || die "Plane API did not return an issue id — response: $response"

    now="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    tmp="$(mktemp)"
    jq --arg id "$item_id" --arg issue_id "$issue_id" --arg now "$now" '
      (.items[] | select(.id == $id) | .plane_issue_id) = $issue_id
      | .updated_at = $now
    ' "$ROADMAP_STATE_FILE" > "$tmp" && mv "$tmp" "$ROADMAP_STATE_FILE"

    log_ok "pushed $item_id to Plane issue $issue_id"
    jq -cn --arg id "$item_id" --arg issue_id "$issue_id" '{item_id: $id, plane_issue_id: $issue_id, status: "synced"}'
    ;;

  pull)
    [[ -n "$item_id" ]] || die "item_id required for pull"
    issue_id="$(jq -r --arg id "$item_id" '.items[] | select(.id == $id) | .plane_issue_id // empty' "$ROADMAP_STATE_FILE")"
    [[ -n "$issue_id" ]] || die "$item_id has no plane_issue_id yet — push first"

    response="$(curl_json GET "${issues_url}/${issue_id}/")"
    jq -cn --arg id "$item_id" --argjson remote "$response" '{item_id: $id, remote: $remote}'
    ;;

  pull-all)
    ids="$(jq -r '.items[] | select(.plane_issue_id != null) | .id' "$ROADMAP_STATE_FILE")"
    results="[]"
    while IFS= read -r id; do
      [[ -z "$id" ]] && continue
      one="$(bash "$0" pull "$id")" || continue
      results="$(echo "$results" | jq -c --argjson one "$one" '. + [$one]')"
    done <<< "$ids"
    echo "$results"
    ;;

  *)
    die "usage: plane_sync.sh {push|pull|pull-all} [item_id]"
    ;;
esac
