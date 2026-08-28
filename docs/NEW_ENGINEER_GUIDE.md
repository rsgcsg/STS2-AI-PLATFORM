# New Engineer Guide

This is the shortest safe path from a fresh checkout to a legitimate first
Platform pull request. It should take roughly 15–30 minutes on a machine with
the portable toolchains installed.

## 1. Know the boundary

Platform connects the real STS2 runtime to fair-player automation,
native-human evidence, evidence logistics, and model-neutral policy lifecycle.
It does not own strategy, rewards, models, training, research admission, or a
second legality engine. STPD is an external research consumer.

Authority is deliberately split:

- STS2: rules, RNG, effects, native legality, and Commit.
- Connector: player-visible Snapshot/Read, complete finite BoundAction,
  execute-time native binding, Receipt, and successor semantics.
- Host Runtime: process lifecycle, isolation, recovery, and exact identity.
- Annotator: native-human witness correlation and immutable recording evidence.
- Evidence: typed verification and immutable transfer, not Human origin or
  research admission.
- Policy Runtime: model-neutral modes and controller/delivery lifecycle over
  Connector-owned actions, not inference or legality.
- Workbench and Live UI: presentation and bounded application commands.
- External consumers: strategy, research projection, training, and evaluation.

If a change would move one of those responsibilities, stop and read
[Architecture](ARCHITECTURE.md) and the relevant ADR before editing.

## 2. Prepare the checkout

Portable CI currently uses Node.js 20, .NET 9, and Python 3.11. The root package
requires Node.js 20 or newer. Git and npm are required; GitHub CLI is useful but
not required for local checks.

```bash
git fetch --prune origin
git switch --create chore/platform/my-change origin/develop
npm ci
```

Use a short-lived branch in its own worktree when another person or agent is
already writing in the repository. Never reuse another workstream's writable
branch.

## 3. Establish the portable baseline

```bash
npm run doctor
npm run check
```

`doctor` reports component prerequisites and may say `action_required` when the
local game/runtime toolchain is absent. The root check is the portable
source/test gate and must not require proprietary STS2 files. If the baseline
is not green, determine whether the failure is pre-existing before changing
code.

Neither command proves build, installation, load, live mutation, a journey,
Human origin, or qualification.

## 4. Choose the owner and reading path

Run a bounded context map:

```bash
npm run project:context
npm run project:context -- --component connector
```

Choose the narrowest component that owns the behavior. Then read, in order:

1. root `AGENTS.md`;
2. the component-local `AGENTS.md` when one exists;
3. the component README or document map;
4. the exact contract, implementation, and neighboring tests.

Local `AGENTS.md` files add subtree-specific safety and evidence rules. They do
not override the root hard shell. Simple presentation leaves may use a README
instead of a local agent guide; `project:context` points to the applicable path.

## 5. Understand evidence language

Evidence levels are distinct:

```text
source -> test -> build -> package -> installed -> loaded
       -> live_exercised -> journey -> human_validated -> qualified
```

Passing one level does not imply the next. A predecessor artifact's report does
not qualify rebuilt bytes. Record exact source/artifact/runtime identities for
game-bound claims and state non-claims explicitly.

## 6. Make one legitimate change

1. Confirm the owning component and dependency direction.
2. Change the smallest relevant implementation or document.
3. Add a positive or fail-closed regression test when behavior changes.
4. Follow `.editorconfig`, compiler/type settings, public wire contracts, and
   the nearest stable code pattern. Do not mass-reformat neighboring code.
5. Run the owning component check and the root portable check.
6. Run `npm run project:closeout` and review every reported impact.
7. Update canonical docs only when their truth changed.
8. Open a PR to `develop` using the repository template.

At minimum:

```bash
npm run project:check
npm run check
npm run project:closeout
git diff --check
```

Game-bound behavior also requires the exact-game and runtime gates named by the
owning component. Do not run deploy/rollback operations merely to validate a
documentation or repository-system change.

## Common traps

- Inventing legality, native operands, coordinates, indexes, or hidden state in
  a consumer.
- Retrying after an `unknown` delivery.
- Treating a workspace commit as every component's semantic identity.
- Copying versions or artifact hashes into a second prose or JSON registry.
- Treating fixtures, compilation, or CI as loaded/Human/qualification proof.
- Loading the entire dated evidence archive for an ordinary code change.
- Putting current branch names, long evidence timelines, or transient blockers
  into durable workflow docs or Skills.
- Committing `.local/`, game files, raw recordings, credentials, build output,
  installed artifacts, or model weights.

## Ready to work

- [ ] I can explain what Platform owns and what it does not.
- [ ] `npm ci`, `npm run doctor`, and `npm run check` have been run.
- [ ] I selected one owning component and read its local guidance.
- [ ] I know the evidence level my change can actually prove.
- [ ] I am on a short-lived branch based on current `origin/develop`.
- [ ] I know the focused check, root check, PR target, rollback, and non-claims.

Use the [Document Map](DOCUMENT_MAP.md) when the task needs a different route.
