#!/usr/bin/env bash
# Dependency Matrix Engine: builds a DAG over state.json's tasks from their
# depends_on edges, plus a heuristic files_scope-overlap conflict report
# (component-boundary collisions, e.g. two tasks touching the same feature
# domain). Topological sort is done in python3 (always present, painful in
# pure bash). Returns JSON on stdout; non-zero exit on a real cycle.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/_lib.sh"
require_jq
require_state_file
command -v python3 >/dev/null 2>&1 || die "python3 is required but not installed"

tasks_json="$(jq -c '.tasks' "$STATE_FILE")"
python3 - "$tasks_json" <<'PYEOF'
import json, sys

tasks = json.loads(sys.argv[1])
by_id = {t["id"]: t for t in tasks}

# Kahn's algorithm over depends_on
in_degree = {t["id"]: 0 for t in tasks}
edges = {t["id"]: [] for t in tasks}
for t in tasks:
    for dep in t.get("depends_on", []):
        if dep not in by_id:
            continue
        edges[dep].append(t["id"])
        in_degree[t["id"]] += 1

queue = [tid for tid, deg in in_degree.items() if deg == 0]
order = []
indeg = dict(in_degree)
while queue:
    queue.sort()
    node = queue.pop(0)
    order.append(node)
    for nxt in edges[node]:
        indeg[nxt] -= 1
        if indeg[nxt] == 0:
            queue.append(nxt)

cycle = len(order) != len(tasks)

blocked = {}
for t in tasks:
    unmet = [d for d in t.get("depends_on", []) if by_id.get(d, {}).get("status") != "done"]
    if unmet:
        blocked[t["id"]] = unmet

def scope_key(glob):
    # collapse a files_scope glob down to its feature-domain directory,
    # e.g. src/ToTen.Api/Features/Marketplace/AcceptOffer/** -> Features/Marketplace
    parts = glob.split("/")
    if "Features" in parts:
        i = parts.index("Features")
        return "/".join(parts[i:i+2])
    return "/".join(parts[:-1]) if glob.endswith("**") else glob

conflicts = []
ids = [t["id"] for t in tasks]
for i in range(len(ids)):
    for j in range(i + 1, len(ids)):
        a, b = by_id[ids[i]], by_id[ids[j]]
        if a.get("status") == "done" or b.get("status") == "done":
            continue
        a_keys = {scope_key(g) for g in a.get("files_scope", [])}
        b_keys = {scope_key(g) for g in b.get("files_scope", [])}
        shared = a_keys & b_keys
        if shared:
            conflicts.append({"tasks": [a["id"], b["id"]], "shared_scope": sorted(shared)})

parallel_eligible = [
    t["id"] for t in tasks
    if t.get("strategy") == "parallel" and t["id"] not in blocked
]

result = {
    "order": order,
    "cycle_detected": cycle,
    "blocked": blocked,
    "conflicts": conflicts,
    "parallel_eligible": parallel_eligible,
}
print(json.dumps(result))
if cycle:
    sys.exit(1)
PYEOF
