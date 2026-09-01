---
name: salvage-branch
description: Diff a specific branch against main and classify its content as superseded, unique-and-valuable, or unique-but-risky, before deciding to merge, cherry-pick, or delete it. Invoke with /salvage-branch <branch-name>.
disable-model-invocation: true
allowed-tools: Read, Grep, Glob, Bash
arguments: [branch]
---

Target branch: `$branch`

1. `git fetch origin`, then `git log origin/main..origin/$branch --oneline` for what it has that `main`
   lacks, and `git diff origin/main...origin/$branch --stat` for scope.
2. Group the diff by feature/concept, not by file. For each distinct piece of functionality, check whether
   an equivalent already exists on current `main` before assuming it's missing — grep `main` for the same
   model/service/component names first.
3. Classify each piece as one of:
   - **Superseded** — `main` already has an equivalent, built differently. Cite the files on both sides.
   - **Unique and valuable** — doesn't exist on `main` in any form, and looks genuinely useful.
   - **Unique but risky** — doesn't exist on `main`, but touches something sensitive (tenant isolation,
     auth, money calculations) and needs careful review before landing, not a blind cherry-pick.
4. If the branch contains a fix for a defect that's also present on current `main` (e.g. a data-isolation
   bug), call that out with the highest priority regardless of how the rest classifies.
5. End with one concrete recommendation: cherry-pick specific commits, reimplement the unique pieces against
   current `main`'s newer code, or discard. Don't take any of these actions yourself — this skill only
   produces the recommendation.
