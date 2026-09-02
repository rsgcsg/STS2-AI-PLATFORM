## Scope

- Repository/base SHA:
- Owning component/workstream:
- Problem and implemented change:
- Non-goals:
- Affected contracts / cross-repository pins:

## Evidence

- [ ] `npm ci`
- [ ] owning component check(s)
- [ ] `npm run check`
- [ ] `npm run project:closeout`
- [ ] `git diff --check`
- [ ] Game-bound source, if changed: `npm run check:exact-game` plus clean exact build/source/artifact identity
- [ ] Runtime/Human gates, only if claimed by this PR, are recorded at their exact source/artifact/runtime scope

CI green is source/test evidence only. Do not promote it to build, installed,
loaded, runtime, Human, or qualification evidence.

## Merge method / component provenance

Choose the applicable statement before merge:

- [ ] This PR changes component source. **Use a normal merge commit; do not use Squash/Rebase merge.** Current path-scoped `source_revision` is commit provenance and squash/rebase rewrites it after CI.
- [ ] This PR changes no component source. Squash is permitted if the final diff/evidence remains coherent.

## Rollback and non-claims

- Rollback:
- Remaining non-claims / future work:
