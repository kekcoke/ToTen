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

## F2 — Warnings (Non-Blocking)

**Node.js 20 deprecation in actions**: `actions/checkout@v4`, `actions/setup-dotnet@v4`,
`azure/login@v2`, `hashicorp/setup-terraform@v3` target Node.js 20 but are being forced to
Node.js 24 by GitHub. These warnings will eventually become errors when GitHub removes
Node.js 20 support. Remediation: bump these actions to their latest versions (v4→latest)
when available with Node.js 24 native support.

**`PostgreSqlBuilder.PostgreSqlBuilder()` obsolete**: Testcontainers deprecation in
`tests/ToTen.Api.IntegrationTests/Helpers/ToTenWebApplicationFactory.cs:23`. Not a CI
failure; address in a follow-up when upgrading Testcontainers.
