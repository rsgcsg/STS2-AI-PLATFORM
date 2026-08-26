# Semantic Boundary Source Closeout

Date: 2026-08-26

## Evidence baseline

The implementation started from Platform source
`1f0315813efc6a65182ba7d17b9656f5f8091cea` and exact STS2 `v0.111.0 /
41cef1ea`, assembly SHA-256
`9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`,
MVID `57785517-0b16-42b9-8b36-bad6fb28384b`.

Latest predecessor owner session
`session-20260826T075502Z-9fe1ac91c78a48f9a8f4eeef204a3665` audits 8/8
strict Decision V2 records, 26 invalidations and ledger v2 counts of 33
accepted, 31 started, 28 finished, five cancelled, eight strict admissions and
25 strict invalidations. Two actions cancelled before start and three after
start. This proves ledger v2 Human/native accounting on artifact
`df5d2c61... / 9072e515...`; it cannot prove the semantic sidecar introduced by
later source.

## Architecture verdict

The old single-pending V2 path is a safe regression adapter, but its complete
interactive successor is too strict to define a universal semantic S. The
minimal sufficient source design is:

```text
exact Human observation H + exact accepted action identity
-> game-owned native lifecycle
-> complete authoritative Player Environment boundary
-> proved S -> A -> S', cancelled/not-successful, or unknown
```

Existing typed lifecycle plus `ActionExecutor.BeforeActionExecuted` proves the
ordering for chained Human actions: capture occurs after prior game-owned work
and before the next tracked Human action effect. A complete interactive/choice
surface proves a non-precommitted boundary. The next Human pre-frame is never
substituted for the prior S'.

Exact source exposed one narrower counterexample: a `PlayCardAction` can return
through `NCardPlayQueue.RemoveCardFromQueueForCancellation(PlayCardAction)`
without `GameAction.Cancel()`, resource spending or `OnPlay`, then report
`Finished`. One read-only exact-overload Prefix classifies that path as aborted
before Commit. No broader Harmony instrumentation, timing heuristic, scheduler
change or gameplay reconstruction is justified.

## Contract and non-claims

`semantic-boundary-trace.jsonl` is additive and independently audited. Decision
V1/V2 bytes, admission and meaning are unchanged. A queued action cancelled
before start or aborted before native Commit is not successful A; cancellation
after start, incomplete capture, persistence uncertainty and unproved ordering
remain unknown. Trace persistence failure is visible in RecordingStatus and is
never retried.

The sidecar is source/test evidence, not loaded, Live, corpus admission,
qualification or training authority. EndTurn full-cycle, natural player choice,
rapid lethal/cross-surface and all Full-Run surfaces require exact-runtime
evidence from the new artifact.

## Final artifact boundary

The source was committed as `d97c89858352a370cf6952d67ee6879b2c2d2f0a`;
BOM alignment is `61d9b36e3f390d54708c5011956fcc405b14961a`.
The clean unified Release artifact is SHA-256
`2cb46ead44ea8d906e7abf834da917f9504bfdc0c6e1a577d152ec3049a5118e`,
MVID `66ed1396-2186-46b0-9fbf-7260c2a2a177`. Built and installed identities
match. Deployment rollback is
`apps/game-mod/.local/deployments/2026-08-26T14-06-26.561Z`.

The artifact cold-loaded in runtime
`af2e7370136549ddbc547a2c9cd3cb13`, exact Modset fingerprint
`3a317ca773d9ddda086019597e11cdecd5785b40bca38554bda339a6525773aa`,
with Player Environment protocol `1.0.0`. RecordingStatus reached Ready with no
implicit session and the startup log contained no patch/Annotator exception.
This proves load and initialization only. Human mutation of the new semantic
trace remains `not exercised`.

## Full-Run seam audit

The Connector already has source-local observations and exact native controls
for map navigation, reward/card reward, event dialogue/options, shop, treasure,
rest and selection surfaces. These are suitable boundary witnesses, not a
universal GameAction promise. Future work should feed each proved surface-ready
or source-local commit witness into the same semantic coordinator while keeping
one definition of S. It must not infer completion from UI shape or create a
second legality/effect model.
