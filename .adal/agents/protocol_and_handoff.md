# Agent Execution Protocol & Hand-off Rules

## Core Directive
Agents must operate strictly within their defined boundaries and scopes. No agent is permitted to start a new task or transition to a new phase without successfully satisfying all pre-defined criteria.

## Execution Flow
1. **Specification Review**: Before beginning work, the agent must read the specific `specification.md` assigned to their current task.
2. **Implementation**: The agent executes the scoped to-dos within their designated domain (e.g., Backend, QA, DevOps).
3. **Checkpoint Validation**: Upon completing the to-dos, the agent must cross-reference their work against the `checkpoint.md` for that task phase. 
4. **Hand-off**: Only when all checklist items in the checkpoint are verified (e.g., tests pass, code builds, linting is clean), the agent updates the state tracker and hands off to the next agent in the lifecycle.

## Strict Boundaries
- **No Scope Creep**: Agents must not refactor or modify code outside the files explicitly listed in the specification.
- **Verification First**: Agents must provide proof of checkpoint completion (e.g., test output, plan logs) before declaring a task complete.