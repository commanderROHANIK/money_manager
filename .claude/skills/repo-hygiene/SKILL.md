---
name: repo-hygiene
description: Reconcile stranded branches and stale issues before an unattended /next-issue run, so it branches off a genuinely current main and picks from a genuinely open issue list. Invoke with /repo-hygiene. This is the skill a nightly scheduled agent should call ahead of next-issue.
disable-model-invocation: true
allowed-tools: Bash, Read, Grep, Glob
---

`next-issue` (`.claude/skills/next-issue/SKILL.md`) trusts two things when it fires: that `main`
actually contains everything that's supposedly landed, and that the open-issue list actually
reflects what's left to do. Both can go stale between runs — a PR merging into a branch that had
already reached `main` leaves work stranded off to the side; an issue's code can land without the
issue itself getting closed. This skill checks both and fixes what it safely can.

## 1. Branches

1. `git fetch origin` first — don't work from stale refs.
2. For every remote branch except `main` and `dependabot/*`, check
   `git merge-base --is-ancestor origin/<branch> origin/main` (same scope `branch-triage` uses).
3. **Stranded work** — the case `branch-triage` reports but doesn't act on: a branch that is
   *not* an ancestor of `main`, but that has `origin/main` as an ancestor of *its own* history
   (`git merge-base --is-ancestor origin/main origin/<branch>` succeeds) — i.e. it was once level
   with `main` (typically because an earlier PR from it, or from a branch it was based on, merged)
   and has since gained commits `main` never received. For each one found:
   - Confirm there is no existing open PR from that branch already covering the gap
     (`gh pr list --state open --head <branch>` or the equivalent `mcp__github__*` list-PRs tool,
     whichever this environment resolves).
   - Open a PR from `<branch>` to `main` (do **not** merge it). Title and body name the earlier
     PR that already reached `main` from this branch's history, and describe what's stranded on
     top of it (`git log origin/main..origin/<branch> --oneline`).
   - Do not force-push, rewrite, or delete anything on the branch itself.
4. Branches with no shared merged history and no open PR at all (genuinely orphaned): report
   only, exactly as `branch-triage` does — recommend `/salvage-branch <name>` in the final
   summary, don't act on them.
5. Branches that are already an ancestor of `main`, or that already have an open PR covering
   their own gap: nothing to do.

## 2. Issues

1. `gh issue list --state open` (or the equivalent `mcp__github__*` tool) for every open issue.
2. For each, read the full issue body (`gh issue view <number>`) — its own Scope/Acceptance
   Criteria section is the bar to check against, not the title.
3. Look for **concrete, verifiable evidence on `main`** that the issue's scope is already done:
   specific files, endpoints, tests matching what the issue asked for — not "something related
   exists." If it's genuinely ambiguous, leave the issue open; a false close is worse than a
   missed one, since `next-issue` will simply pick it up next time either way.
4. Close each conclusively-resolved issue with a comment that names exactly what was checked
   against the issue's own acceptance criteria (cite file paths / test names), the same standard
   used for issue #8 on 2026-09-02.
5. Never close an issue on a guess, and never edit or reopen one — only close, with the
   explaining comment.

## 3. Report

End with exactly one `PushNotification` summarizing what was found and done (or that nothing
needed doing) — bridging PRs opened, issues closed, orphaned branches flagged. This runs
unattended, so the durable record (the PRs and issue comments themselves) needs a signal pointing
at it; don't rely on the session transcript being read.

## Never

Never merge, delete, or force-push anything, and never commit directly to `main`. The only write
actions this skill takes are opening a PR and closing/commenting on an issue.
