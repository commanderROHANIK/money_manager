---
name: next-issue
description: Pick up the next open GitHub issue and work it end-to-end on a fresh branch — implement, test, open a PR. Invoke with /next-issue [issue-number] (omit to pick the oldest open issue). This is the skill a scheduled/unattended agent should call.
disable-model-invocation: true
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
arguments: [issue_number]
---

Issue: `$issue_number` (if empty, run `gh issue list --state open --json number,title,createdAt` and pick
the oldest).

1. `gh issue view <number>` to read the full issue body, not just the title.
2. Create a **fresh branch per issue**: `claude/issue-<number>-<short-slug>`, off current `main` — do not
   reuse a branch from a previous issue, so more than one issue can be in flight across sessions without
   collision.
3. Read `CLAUDE.md` before touching `Data/`, `Migrations/`, or the analytics calculator, per its own
   instruction.
4. Implement the issue, following existing patterns — see the `add-resource` skill for the
   model/DbSet/migration/controller shape if the issue needs a new resource.
5. Run the relevant checks before opening a PR: `dotnet build app.sln`, `dotnet test app.sln`, and for UI
   changes `npm run lint && npm run typecheck && npm run test:coverage` in `money-manager-ui`. Don't open a
   PR with failing checks.
6. Run the `tenant-isolation-check` skill if the change touches any owned entity or controller.
7. `git push -u origin <branch>` then `gh pr create` with a body that includes `Closes #<number>` and fills
   out the repo's PR template honestly (verification commands actually run, the checkbox list, "Authored
   with AI assistance: yes").
8. Stop after opening the PR — don't merge it yourself. Report the PR URL and a short summary of what was
   implemented.

If the issue is too ambiguous to implement confidently, stop and report back what's unclear rather than
guessing.
