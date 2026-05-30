# Spec & Checkpoint Sync

## Purpose
After completing implementation and before committing, synchronize `phases/{N}/spec.md` and `phases/{N}/checkpoint.md` so that every completed item is marked `[x]`. If items are missing from those files, append them. If the files themselves are missing, create them. The implementation code and the updated tracking files must land in the same commit.

## When to Apply
After all implementation steps for the current session are complete and before running `git add` / `git commit`. Apply every session without exception — even if you believe no items changed.

## Step 1 — Resolve Phase Number N

Use the first matching rule:

1. **Branch name** — run `git branch --show-current`. If it matches `implement/phase-{N}`, extract `N`. Stop here.
2. **Implementation plan heading** — scan the `docs/adal/` plan file used this session for a `# Phase {N}` heading or `**Phase**: {N}` field.
3. **Files modified** — cross-reference modified paths against `generic_specification.md` phase assignments (Terraform/infra → Phase 3; API slices → Phase 2; EF models/migrations → Phase 1; CI/CD → Phase 4; tests → Phase 5; Gateway/AI → Phase 6).
4. **Explicit agent instruction** — use the phase stated in the session prompt.

If N cannot be resolved unambiguously, output: `"Cannot determine target phase. Spec/checkpoint sync skipped. Manually verify phases/{N}/spec.md and phases/{N}/checkpoint.md."` Then stop.

## Step 2 — Check File Existence

- If `phases/{N}/spec.md` does not exist → run **Seed Procedure** (Section below) for spec.
- If `phases/{N}/checkpoint.md` does not exist → run **Seed Procedure** for checkpoint.

## Step 3 — Core Sync Algorithm

Run this algorithm independently for `phases/{N}/spec.md` and `phases/{N}/checkpoint.md`.

**A. Enumerate implemented items** from the session — the to-dos completed, verified, or committed this session. Each item has a prose label.

**B. For each item, classify against the file:**
- `- [x]` match found → skip (already marked).
- `- [ ]` match found → change to `- [x]`. Preserve all trailing text including parenthetical notes like `*(Requires Azure credentials.)*` verbatim.
- No match → queue for append (Step C).

Use normalized matching: ignore leading/trailing whitespace, case, and parenthetical suffixes. The substantive prose must correspond to a real unit of completed work. Do not use partial substring matches on short labels.

**C. Append missing items** under the correct phase section header (e.g., `## Phase 3: Infrastructure as Code`). Format each as:
```
- [x] {prose label}
```
If the correct section header is absent, prepend it before appending the items. Only append items concretely completed this session — never speculative or planned items.

**D. Verify** — read the file back and confirm every item from Step A is now `[x]`. If any remain `[ ]`, repeat Step B for those items.

## Step 4 — Multi-Phase Sessions

If the session touched items across multiple phases, apply Steps 2–3 separately for each affected N. Update all involved `phases/{N}/` files before staging.

## Seed Procedure (for missing files)

**If `phases/{N}/spec.md` is missing:**
1. Open `.adal/agents/generic_specification.md`.
2. Find the `## Phase {N}:` section. Copy that section (header + objective + all `- [ ]` items) into a new `phases/{N}/spec.md`.
3. Add an overview block matching the format in `phases/1/spec.md` lines 1–5.
4. If Phase {N} has no entry in `generic_specification.md` (phases 1–3 have already been extracted), create a minimal file with the section header from the agent's current scope and no pre-populated unchecked items.
5. Apply the Core Sync Algorithm to mark completed items in the newly seeded file.

**If `phases/{N}/checkpoint.md` is missing:** same procedure using `.adal/agents/generic_checkpoint.md` as source.

**If `phases/{N}/` directory is missing:** create the directory, then seed both files.

## Edge Cases

- **Parenthetical environment notes** — preserve exactly: `*(Requires live Azure subscription.)*`
- **Same item appears in multiple phase sections** — only update the section matching resolved N.
- **Idempotency** — running this skill twice on the same session must produce the same output as running it once. Already-`[x]` items are skipped; append only adds items not yet present.
- **CRLF files** — preserve existing line endings; do not convert.

## Commit Requirement

Stage the updated `phases/{N}/spec.md` and `phases/{N}/checkpoint.md` alongside the implementation files. These must appear in the same commit (or an immediate companion `docs` commit on the same branch). The commit message body must include:
```
docs(phase-{N}): update spec and checkpoint against full implementation
```
The tracking files must never lag behind the code changes they document.
