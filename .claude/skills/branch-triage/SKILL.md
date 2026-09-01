---
name: branch-triage
description: List remote branches not merged into main and cross-reference open PRs, flagging orphaned cloud-agent work. Invoke with /branch-triage to catch work that never got reviewed or merged.
disable-model-invocation: true
allowed-tools: Bash
---

For every remote branch other than `main` and `dependabot/*`:

1. `git fetch origin` first — don't work from stale refs.
2. For each branch, check `git merge-base --is-ancestor origin/<branch> origin/main` — MERGED if it succeeds.
3. For branches not merged, check `gh pr list --state all --search "head:<branch>"` (or filter full
   `gh pr list --state all --json number,title,headRefName,state`) to see if it has an open, closed, or no PR.
4. Report a table: branch | status (merged / open PR #N / closed-unmerged PR #N / orphaned — no PR at all) |
   last commit date (`git log -1 --format=%cd origin/<branch>`) | rough size
   (`git diff origin/main...origin/<branch> --stat` — line count and file count).
5. For anything **orphaned** (no PR, not merged), flag it clearly and suggest running
   `/salvage-branch <name>` next rather than deciding its fate here.

Don't merge, delete, or open anything yourself — this skill only reports.
