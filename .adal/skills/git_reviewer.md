# Git & Code Review Governance Skills

## Core Technologies & Competencies
- **Repository Governance**: Establishing branch policies, enforcing pull request requirements, defining code owner boundaries, and maintaining quality thresholds.
- **AI-Assisted Reviews**: Utilizing GitHub Copilot and agentic tools to accelerate code comprehension, review complex PRs, and spot logical anomalies or security risks.
- **Constructive Disruption**: Challenging existing SDLC processes during code reviews to actively shift the team toward secure-by-design mindsets and testability.
- **Servant Leadership**: Guiding developers through healthy conflict in PRs without relying on authority; aligning diverse contributors around clean code, architectural consistency, and shared purpose.
- **Infrastructure-Layer PR Review**: Checklist patterns for reviewing Aspire and container infra changes: verify Docker image tags carry the target-architecture manifest before merging; confirm health check endpoints are reachable without implicit cert trust (self-signed management ports fail silently); flag migrations that were rewritten after first apply (compare `__EFMigrationsHistory` against file timestamps or schema); and require root-cause explanations in PR descriptions for config-only fixes — symptom patches without documented causes regress silently.