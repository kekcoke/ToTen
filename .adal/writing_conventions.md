# Agent Writing Conventions

All agents must strictly adhere to the following writing conventions when interacting with version control, issue trackers, and documentation. This ensures uniformity, predictability, and clean automation triggers across the ecosystem.

## 1. Commit Messages (Conventional Commits)
Agents must use the [Conventional Commits](https://www.conventionalcommits.org/) format. This enables automated semver bumping and changelog generation.

**Format**:
`<type>(<scope>): <subject>`
`<blank line>`
`<body>`
`<blank line>`
`<footer>`

**Allowed Types**:
- `feat`: A new feature
- `fix`: A bug fix
- `docs`: Documentation only changes
- `style`: Changes that do not affect the meaning of the code (white-space, formatting, etc.)
- `refactor`: A code change that neither fixes a bug nor adds a feature
- `test`: Adding missing tests or correcting existing tests
- `chore`: Changes to the build process or auxiliary tools and libraries

**Rules**:
- The `<subject>` line must use the imperative mood (e.g., "add feature", not "added feature").
- The `<subject>` line must not exceed 50 characters.
- The `<body>` must explain *why* the change was made and *what* it does, not *how* it does it.
- The `<footer>` must include related issues (e.g., `Closes #123`).

## 2. Issue Formatting
When creating or updating issues, agents must use structured templating.

**Structure**:
- **Title**: `[Type] Brief description of the issue` (e.g., `[Feature] Add OpenTelemetry exporter`)
- **Context/Problem**: Explain why this work is needed or the bug being experienced.
- **Acceptance Criteria (AC)**: A strict, bulleted list of verifiable conditions that must be true for the issue to be considered complete.
- **Technical Notes**: Any architectural constraints, required tools, or specific agent bounds.

## 3. Merge / Pull Requests (PRs)
PRs are the primary gate for the Git Reviewer and DevOps agents.

**Structure**:
- **Title**: Must follow the Conventional Commits format (usually matches the primary commit).
- **Summary**: A brief overview of what changed and why.
- **Related Issues**: Explicit links using keywords like `Resolves #X` or `Fixes #Y`.
- **Impact Area**: Define what systems, slices, or components are affected by this PR.
- **Agent Checklist**:
  - [ ] Code follows project style guidelines.
  - [ ] Ephemeral tests (Unit/Integration) run and pass.
  - [ ] CHANGELOG.md updated (if applicable).
  - [ ] No secrets or hardcoded credentials committed.

## 4. Changelog Generation
We follow the [Keep a Changelog](https://keepachangelog.com/) standard.

**Rules**:
- Agents must update the `CHANGELOG.md` in the same PR as their feature or fix.
- All new entries must go under the `[Unreleased]` section.
- Organize changes under the following headers:
  - `### Added` for new features.
  - `### Changed` for changes in existing functionality.
  - `### Deprecated` for soon-to-be removed features.
  - `### Removed` for now removed features.
  - `### Fixed` for any bug fixes.
  - `### Security` in case of vulnerabilities.
- Entries should be concise and reference the PR or Issue number where possible.