## What and why

<!-- One paragraph. What changed, and what problem it solves. Link the issue if there is one. -->

## How to verify

<!-- The exact commands a reviewer should run, and what they should see. -->

```bash
```

## Checklist

- [ ] Tests added or updated for the behaviour that changed
- [ ] `dotnet test app.sln` run locally — or stated below that it was not
- [ ] `npm test` run locally — or stated below that it was not
- [ ] No invariant in `CLAUDE.md` weakened
- [ ] No existing assertion deleted or loosened to make a check pass
- [ ] No `eslint-disable` / `#pragma warning disable` / `!` added to silence a check
- [ ] Migration + `.Designer.cs` + model snapshot committed together, if the model changed
- [ ] No new outbound network dependency

## Verification honesty

<!--
The most useful line on this form when reviewing agent-authored work. Be specific.
Which commands did you actually run, and which parts are unverified?
-->

Authored with AI assistance: **yes / no**

Not manually verified:

## Screenshots

<!-- For UI changes. Before and after. -->
