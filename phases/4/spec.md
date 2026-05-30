## Phase 4: DevSecOps & CI/CD Integration
**Assigned Agent**: DevSecOps Agent
**Objective**: Automate delivery, enforce quality gates, and embed security by design.
**To-Dos**:
- [x] Rewrite `.github/workflows/azure-dev.yml` to utilize `hashicorp/setup-terraform`.
- [x] Add SAST/DAST scanning steps (GitHub Advanced Security / OWASP ZAP) to pull requests.
- [x] Configure Docker Build & Push to Azure Container Registry (ACR).
- [x] Implement pipeline deployment steps using Terraform apply and Azure CLI container app updates.
- [x] Configure CI pipelines to pack and publish `Shared` and `Contracts` as reusable internal NuGet packages.

### Additional Implementation Items (not in original spec)
- [x] Create multi-stage Dockerfiles for `ToTen.Api` (`docker/api/Dockerfile`) and `ToTen.Worker` (`docker/worker/Dockerfile`), built from repo root.
- [x] Create `terraform/modules/apps/` module provisioning `ToTen.Api` and `ToTen.Worker` as ACA container apps with secrets, health probes, and ingress configuration.
- [x] Update `terraform/main.tf`, `variables.tf`, and `outputs.tf` to wire the `apps` module into the root Terraform stack.
- [x] Create `.github/workflows/codeql.yml` as a dedicated SAST workflow with weekly schedule and C# language matrix.
- [x] Create `.zap/rules.tsv` as the OWASP ZAP rule suppressions file.
