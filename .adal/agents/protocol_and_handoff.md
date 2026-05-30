# Agent Execution Protocol & Hand-off Rules

## Core Directive
Agents must operate strictly within their defined boundaries and scopes. No agent is permitted to start a new task or transition to a new phase without successfully satisfying all pre-defined criteria.

## Execution Flow
1. **Specification Review**: Before beginning work, the agent must read the specific `specification.md` assigned to their current task.
2. **Implementation**: The agent executes the scoped to-dos within their designated domain (e.g., Backend, QA, DevOps).
3. **Spec & Checkpoint Sync**: Immediately after completing all implementation to-dos and before reading the checkpoint for validation, every agent MUST execute the `spec_checkpoint_sync.md` skill. This marks completed items as `[x]` in `phases/{N}/spec.md` and `phases/{N}/checkpoint.md`, appends any implemented items not present in those files, and creates the phase files if they do not exist. This step is mandatory — skipping it is a protocol violation. The updated tracking files must be staged in the same commit as the implementation.
4. **Checkpoint Validation**: The agent cross-references their work against the updated `checkpoint.md` for that task phase.
5. **Hand-off**: Only when all checklist items in the checkpoint are verified (e.g., tests pass, code builds, linting is clean), the agent updates the state tracker and hands off to the next agent in the lifecycle.

## Strict Boundaries
- **No Scope Creep**: Agents must not refactor or modify code outside the files explicitly listed in the specification.
- **Verification First**: Agents must provide proof of checkpoint completion (e.g., test output, plan logs) before declaring a task complete.
- **Sync Before Validate**: Agents must not run Checkpoint Validation (Step 4) without first completing the Spec & Checkpoint Sync (Step 3). Validating against stale tracking files produces unreliable results.