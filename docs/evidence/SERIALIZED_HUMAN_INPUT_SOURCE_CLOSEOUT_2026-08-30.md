# Serialized Human Input Source Closeout - 2026-08-30

## Baseline

The immutable owner session
`session-20260829T084437Z-cc4079776c9e417eba53a122e452cab7`
remains the exact predecessor baseline. It binds artifact
`bb37d34f... / 3587836e...`, runtime `9a42d54c...`, sole-Platform Modset
`90f3c7f3...` and exact STS2 `v0.111.0 / 41cef1ea`.

Its 933 accepted roots all receive trace-level dispositions, but canonical
calibration yields zero complete one-step rows. It performs 31,613 synchronous
Player Environment captures, taking 628.720 seconds cumulatively (27.519% of
the 2,284.687-second session) with a 273.851 ms maximum. The three polling
phases alone account for 25,188 captures and 495.870 seconds. Buffered evidence
append is only 0.615 seconds.

## Causal repair

The new collection lane is:

```text
complete S + A(S)
-> admit one mutation-producing Human input
-> exact native acceptance/direct Commit
-> game-owned terminal lifecycle
-> next mutation edge or Close captures one complete authoritative boundary
-> persist Decision V2 compatibility plus canonical S/A/S' references
-> reopen mutation input
```

The admission policy is stateless. Existing `_pending`, native lifecycle and
the source-local direct-Commit witness remain the only state owners. A second
mutation is blocked while native lifecycle is open. Once lifecycle is terminal,
one Read-rich boundary both settles the previous action and becomes the exact
pre-frame for the next action. No input is queued, replayed, synthesized or
reordered by Platform.

High-frequency canonical families no longer feed the schema-3 tracker in
parallel. This removes its execution-time full-frame capture and duplicate
freeze/Read persistence. The schema-3 path remains only for Full-Run families
not yet migrated to the canonical lane. PlayCard's exact pre-Commit abort seam
directly invalidates the canonical candidate. Cancellation, persistence
uncertainty, incomplete boundaries and Close timeout remain explicit unknown;
none are retried from a later frame.

Close now requests its boundary on the first command. It performs at most one
authoritative capture after lifecycle terminal; unavailable evidence becomes
an explicit invalidation and the session closes instead of waiting for a second
Close command. The five-second drain limit only produces unknown and never
proves settlement.

## Evidence representation

`canonical-transitions.jsonl` is an additive stream. Every row binds one
Decision V2 action to content-addressed pre/successor frames and records the
serialized-lane invariants. Independent audit verifies the file digest,
snapshot identity, exact Decision/action match, pre-frame content and successor
content. Historical sessions without this stream keep their original meaning.

This avoids redefining Decision V2 or schema 3. STPD research projection and
Parquet/Zstd remain downstream derived artifacts, not Recorder authority.

## Automated evidence

- Annotator Core: 91 tests pass, including serialized admission, canonical
  validation, exact store/audit binding, tamper rejection and predecessor
  compatibility.
- Annotator Node/boundary: 12 + 4 tests pass.
- exact-game Annotator and unified Mod builds pass against the exact assembly.
- boundary checks reject reintroduced per-frame semantic polling, parallel
  canonical/schema-3 publication and non-gated mutation Prefixes.

The structural upper bound is no longer polling-frequency dependent. In the
steady canonical lane, one Read-rich boundary is shared between predecessor
settlement and next-action admission; status capture is explicit. This is
source/test evidence, not a measured after-latency result.

## Remaining gate

The clean candidate is now built, installed and cold-loaded as artifact
`b805474d... / 3ab1e10e...`; see the
[runtime candidate](SERIALIZED_HUMAN_INPUT_RUNTIME_CANDIDATE_2026-08-30.md).
One owner session remains. It must show canonical rows, no false transition,
first-command Close, explicit blocked rapid input, and materially lower
input-adjacent/cumulative capture cost. No predecessor Live claim transfers to
that artifact.
