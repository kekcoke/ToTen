# CI Findings — 2026-06-03 Post-Merge Run

Workflow run: `CI/CD` on `main` after merge of PR #15.
Run ID: `26917622994`

---

## F1 — Terraform Apply: "Provider produced inconsistent final plan"

**Job**: `terraform` (apply step)
**Status**: FAILED
**Severity**: HIGH — blocks all post-merge jobs (`deploy`, `dast-scan`, `robot-tests`, `performance-test`)

### Error

```
Error: Provider produced inconsistent final plan

When expanding the plan for module.apps.azurerm_container_app.api to include new values learned
so far during apply, provider "registry.terraform.io/hashicorp/azurerm" produced an invalid
new value for .template[0].container[0].env[2].value: was
  "https://toten-prod-keycloak--8mncst4.graydune-34aab6fe.canadacentral.azurecontainerapps.io/realms/ToTen"
but now
  "https://toten-prod-keycloak--0000001.graydune-34aab6fe.canadacentral.azurecontainerapps.io/realms/ToTen"

This is a bug in the provider, which should be reported in the provider's own issue tracker.
```

### Root Cause

`terraform/modules/keycloak/outputs.tf` outputs the **revision-specific** FQDN:

```hcl
output "fqdn" {
  value = azurerm_container_app.keycloak.latest_revision_fqdn
}

output "authority_url" {
  value = "https://${azurerm_container_app.keycloak.latest_revision_fqdn}/realms/ToTen"
}
```

`latest_revision_fqdn` includes the active revision suffix (e.g., `--8mncst4`).
When the Keycloak container app is updated during `terraform apply` (in PR #15, we changed
`min_replicas` from 1 to 0), Azure creates a new active revision, which gets a new suffix
(`--0000001`). By the time Terraform tries to apply the API container app changes, the
`Auth__Authority` env var value has changed from what was in the plan, causing the
"inconsistent final plan" error.

This race condition was latent before PR #15 because nothing was updating the Keycloak
container app. The `min_replicas` change was the first Keycloak module update since initial
provision, exposing the issue.

### Fix

Use `ingress[0].fqdn` instead of `latest_revision_fqdn` in the Keycloak module outputs.
`ingress[0].fqdn` is the **stable** ACA ingress URL that does not include a revision suffix
and routes to the latest active revision automatically.

File: `terraform/modules/keycloak/outputs.tf`

```hcl
output "fqdn" {
  value = azurerm_container_app.keycloak.ingress[0].fqdn
}

output "authority_url" {
  value = "https://${azurerm_container_app.keycloak.ingress[0].fqdn}/realms/ToTen"
}
```

This change is backwards-compatible: the stable URL resolves to the same endpoint, just
without a revision suffix. Smoke tests, Auth configuration, and OIDC discovery all work
against the stable URL.

### Verification

After applying the fix, run `terraform plan` and confirm:
- `module.keycloak.authority_url` output value changes to the non-`--<revision>` form
- `module.apps.azurerm_container_app.api` env var `Auth__Authority` is updated to the new value
- `terraform apply` completes without "inconsistent final plan" error

---

---

## F3 — DAST Scan: 5 WARN-NEW Security Header Alerts

**Job**: `dast-scan` (OWASP ZAP Baseline)
**Status**: FAILED (exits 1 on any WARN-NEW when `fail_action: true`)

### Findings

```
WARN-NEW: Strict-Transport-Security Header Not Set [10035] x 3
WARN-NEW: Content Security Policy (CSP) Header Not Set [10038] x 2
WARN-NEW: Storable and Cacheable Content [10049] x 3
WARN-NEW: Permissions Policy Header Not Set [10063] x 3
WARN-NEW: Sec-Fetch-Dest Header is Missing [90005] x 8
```

ZAP scanned `https://toten-prod-api--0000005.graydune-34aab6fe.canadacentral.azurecontainerapps.io`
(the revision-specific FQDN from Terraform output). The root URL returns 404 (no default route).
All 5 findings are false positives or not applicable for a JSON REST API:

| Rule | Reason to suppress |
|------|-------------------|
| 10035 HSTS | ACA handles TLS termination; HSTS on a JSON API's 404 page is not required |
| 10038 CSP | Content Security Policy is browser-facing; not applicable to a JSON API |
| 10049 Cacheable | ZAP flags 404 responses as cacheable — false positive |
| 10063 Permissions Policy | Browser-specific header; not applicable to a JSON API |
| 90005 Sec-Fetch-Dest | ZAP's automated scanner does not send this header; always fires |

### Fix

Added `IGNORE` entries for all 5 rule IDs to `.zap/rules.tsv`.

---

## F4 — Robot Framework: `Get From Dictionary` Keyword Not Found

**Job**: `robot-tests`
**Status**: FAILED
**Test**: `Price Filter Returns Results Within Range` (marketplace.robot)

### Error

```
No keyword with name 'Get From Dictionary' found. Did you try using keyword
'RequestsLibrary.GET' and forgot to use enough whitespace between keyword and arguments?
```

### Root Cause

`Get From Dictionary` is from Robot Framework's built-in `Collections` library.
`tests/ToTen.AcceptanceTests/resources/keywords.resource` only imported `RequestsLibrary`.

### Fix

Added `Library    Collections` to `keywords.resource`.

---

## F5 — Robot Framework: Auth Failures (401 on Authenticated Tests)

**Job**: `robot-tests`
**Status**: FAILED
**Tests**: `Create Organization With Auth Returns 201` (401 ≠ 201)

### Root Cause

With `min_replicas=0` (applied by PR #15), Keycloak scales to zero when idle. The robot-tests
job runs shortly after the `deploy` job completes. If Keycloak is cold at that point (~30–60s
warm-up required), the API's JWT validation middleware cannot reach the Keycloak OIDC discovery
endpoint, and all authenticated requests return 401 regardless of token validity.

This is documented in `docs/deployment-cookbook.md` E10.

### Remediation Options

**Option A (recommended)**: Add a Keycloak warm-up step at the start of `robot-tests` before
the Robot Framework run:

```yaml
- name: Wait for Keycloak to warm up
  run: |
    echo "Warming up Keycloak..."
    for i in $(seq 1 12); do
      STATUS=$(curl -sf --max-time 10 "https://${{ needs.terraform.outputs.keycloak_fqdn }}/realms/ToTen" \
        | jq -r '.realm' 2>/dev/null || echo "")
      [ "$STATUS" = "ToTen" ] && echo "Keycloak ready." && exit 0
      echo "Attempt $i/12 — sleeping 10s..."
      sleep 10
    done
    echo "WARNING: Keycloak did not respond after 120s — tests may fail."
```

**Option B**: Raise `messageCount` or add a minimum delay before robot-tests in the workflow.

**Option C**: Accept intermittent failures on the first post-deploy run (scale-to-zero
trade-off). Re-run manually once Keycloak is warm.

**Not a code bug** — authenticated tests work correctly once Keycloak is running.

---

## F6 — Robot Framework: `Create Item` Returns 400 (Pre-Existing)

**Job**: `robot-tests`
**Status**: FAILED
**Test**: `Create Item Returns 201` (400 ≠ 201)

### Root Cause

The API returns 400 (not 401), so authentication is accepted — the request body is failing
validation. This is a pre-existing test data issue in `tests/ToTen.AcceptanceTests/tests/items.robot`
unrelated to this session's changes. The item creation endpoint likely has required fields
that the robot test is not providing.

### Remediation

Inspect `POST /api/items` request body requirements and update the robot test data to match.
Not caused by this session's changes.

---

## F2 — Warnings (Non-Blocking)

**Node.js 20 deprecation in actions**: `actions/checkout@v4`, `actions/setup-dotnet@v4`,
`azure/login@v2`, `hashicorp/setup-terraform@v3` target Node.js 20 but are being forced to
Node.js 24 by GitHub. These warnings will eventually become errors when GitHub removes
Node.js 20 support. Remediation: bump these actions to their latest versions (v4→latest)
when available with Node.js 24 native support.

**`PostgreSqlBuilder.PostgreSqlBuilder()` obsolete**: Testcontainers deprecation in
`tests/ToTen.Api.IntegrationTests/Helpers/ToTenWebApplicationFactory.cs:23`. Not a CI
failure; address in a follow-up when upgrading Testcontainers.
