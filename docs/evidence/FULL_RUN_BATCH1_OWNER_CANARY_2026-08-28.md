# Full-Run Batch 1 Owner Canary

## Identity

- Session: `session-20260827T140555Z-4ad2b05c732f4fafa5a600b2dddcdfd8`
- Timeline: `timeline-6f67c6691ec04504ab11716fb2476457`
- Unified artifact: `fe3e3a82cdf84cdaa30dea9f5ed0d65fc856099b8391fc165f833b5a57831796`
- Artifact MVID: `b1284288-3a82-4369-b548-a0220793b80e`
- Annotator source: `509e5c6f51a7c68353673a189b7f480d78aa11f7`
- Runtime instance: `d863194aff7a481d8afa9fdb3f1a56f7`
- Environment fingerprint: `12acc6d8b4d8296c58537f6934edb6afb268a873baf9d94f5e0b85b63677ec6e`
- STS2: `v0.111.0 / 41cef1ea`
- Modset: sole exact `STS2_PLATFORM`, fingerprint
  `047da747a425d0177bfa9868555a5119dff1a6d1451066a02069f3d8112ad8c2`

This is owner-attested Human runtime evidence. It is not exhaustive Combat or
Full-Run qualification.

## Result

The canary is **failed for semantic accounting**, despite 404 valid Decision V2
records and 278 explicit invalidations. It exercised three runs and reached
combat, generated-card choice, reward claim, card reward, reward proceed and
map travel.

Before the trace stopped, it recorded 157 accepted semantic actions: 101 card
plays, 24 end turns, two generated-card selections, 12 reward claims, five card
reward selections, five reward proceeds and eight map actions. Their semantic
dispositions were 134 proved, 22 unknown and one cancellation before start.

All 22 unknowns were complete direct-UI actions whose execution capture used a
private witness name that schema 2 did not recognize as an execution boundary.
They are one implementation defect, not 22 independent information gaps.

The first causal failure was more serious. A parent `GameAction` reached a
player-choice semantic disposition while its native lifecycle was paused. Child
acceptance pruned the parent before native resume/finish. The later `Finished`
callback then raised `Unknown semantic action witness` and disabled semantic
trace collection. The native ledger continued to 671 accepted roots while the
semantic trace retained only 125 corresponding game-action roots.

The previous audit returned PASS because it validated each sidecar internally
but did not compare their accepted-root coverage. The strengthened audit now
returns FAIL with 546
`semantic_trace_missing_accepted_native_action` findings. Raw evidence was not
changed.

## Repair Candidate

Source `c8775e1066137c1a7e00993a7ab74493a11717f7`:

- retains semantically disposed parents until native terminal lifecycle;
- gives direct UI commits the canonical execution-boundary witness;
- rejects schema-2 recordings with missing native accepted-root accounting;
- adds exact direct-UI Combat hand select, replace/deselect and confirm
  witnesses without changing Connector legality or operands.

Clean unified artifact:

- SHA-256: `8d2f7d2a8e95eac424aa7fed7f22e825821609b83526d38605e813b6a9692c35`
- MVID: `3043f4f4-63c8-4058-8f4e-44b60801d3d5`

This repair has source/test/build/install/load evidence. Safe install created
rollback `apps/game-mod/.local/deployments/2026-08-27T15-04-13.434Z`. Cold-load
verification passed in runtime `fb5a82ea198140aebfcdbe92b654fce1`, environment
`4866d18435e47f10970999ce4111dc51e575b1b9696b5ddf1b4dced04d4ff259`,
with sole exact Modset fingerprint
`a66aef087216f2ffdf4e5e87d849f1ffa3df2adc073b1b1651801886dabc3281`.
Human runtime evidence is pending and no predecessor evidence transfers.
