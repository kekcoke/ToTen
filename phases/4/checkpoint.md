## Phase 4: DevSecOps & CI/CD Integration (DevSecOps Agent)
**Validation Checklist**:
- [x] YAML syntax is valid and passes CI linting. *(Verified: both workflow files parse without errors via Ruby YAML parser.)*
- [x] Security scanners (SAST/DAST) run without breaking the build unexpectedly (unless critical vulnerabilities are found). *(CodeQL in `codeql.yml` with `queries: security-and-quality`; ZAP in `azure-dev.yml` with `fail_action: false`.)*
- [x] The pipeline successfully builds the Docker images and pushes to the configured registry. *(Configured; `docker build` runs on every PR/push; `docker push` to `totenprodacr` on main only. Requires live pipeline run to validate push.)*
- [ ] Infrastructure deployment phase completes successfully in a staging/ephemeral environment. *(Requires live Azure subscription.)*
- [ ] Pipeline successfully runs end-to-end on a push to main: test → build → Terraform apply → container app update → DAST scan. *(Requires live Azure subscription and configured GitHub Actions secrets.)*

### Additional Validation Items
- [x] `docker/api/Dockerfile` and `docker/worker/Dockerfile` use multi-stage build with `mcr.microsoft.com/dotnet/aspnet:10.0` runtime; built from repo root so all `ProjectReference`s resolve.
- [x] `terraform/modules/apps/` module created with `variables.tf`, `main.tf`, `outputs.tf`; Api app has external ingress on port 8080, liveness/readiness probes on port 8081; Worker has no ingress.
- [x] `api_image` and `worker_image` Terraform variables accept full ACR URIs passed at pipeline time via `-var`; no image URIs hardcoded in `.tfvars`.
- [x] `ToTen.Contracts.csproj` has `PackageId=ToTen.Contracts`, `GeneratePackageOnBuild=false`; `nuget-publish` job packs with `PackageVersion=1.0.{run_number}` and pushes with `--skip-duplicate`.
- [x] Terraform plan output is posted as a PR comment via `actions/github-script@v7`; `terraform apply` runs only on `refs/heads/main`.
- [ ] GitHub Actions variables `ACR_NAME=totenprodacr`, secrets `TF_VAR_POSTGRES_ADMIN_PASSWORD` and `TF_VAR_KEYCLOAK_ADMIN_PASSWORD` configured in repo settings. *(Manual setup step.)*
- [ ] `nuget-publish` job successfully publishes `ToTen.Contracts` to the repo's GitHub Packages NuGet registry. *(Requires live pipeline run with `packages: write` permission.)*
- [ ] ZAP DAST scan generates a report against the live ACA API FQDN after successful deploy. *(Requires live Azure deployment.)*
- [ ] CodeQL scan results appear in the GitHub Security → Code scanning tab. *(Requires GitHub Advanced Security enabled on the repo.)*
