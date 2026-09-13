# First dedicated Human Close-to-R2 audit

This bounded Human gate passed. Recording, transfer and research admission remain
separate authorities. The operator explicitly authorized the dedicated new campaign
and attested Human origin; that attestation is not machine proof.

## Exact historical evidence

- Platform audit/tool workspace: `d09552130d5aad0639b5d5358bc3acfed49ba493`.
- STPD receiver/consumer: `99aa8199dbbcee7c600fcdb5b923168214e92635`;
  lock SHA256 `7d7189706458ce21f73c68688995f5d6ec1cac3bcf29e4cee2068658749db9a2`.
- Native Mod SHA256: `af4dfd93a4236b50aadbff8e9c055a519b20e842c582ab392d7748845df40cc8`;
  MVID `1d53eae8-3353-4802-88fa-0eb3538fa217`. Game v0.111.0 / `41cef1ea`.
- Collection-tool release: `46a832f30ab97ba2dab34879d1cf70d95f6ede67dcf40f1c31b4900929ec13a4`.
- Bundle content: `eb9f9498bc0eba0a4ca0a024fcdde358e9401ed2ab80a6588b979f2d8f199c6d`.
- Archive SHA256: `3db85d2ba71eb5eb5475b6d120adb6fe1411c4ffadc7da736c1bc1243286a308`;
  2,174,883 bytes compressed, 1,491 members / 31,612,434 expanded bytes.
- Remote immutable artifact: `ead7820ccc14c0217f2b2be3d49e3d542fb3b53376bd1576cc42d7e23d471428`.

The raw 1,486 files / 30,146,121 bytes were inventoried before and after audit:
unchanged. Raw recordings, private campaign/runtime identifiers, credentials and
downloaded payloads remain outside Git. The fixed release tool and current local
audit agree; existing native compiled bytes are unchanged by delivery repairs.

## Recording accounting

| Authority | Observed result |
|---|---|
| Accepted decisions | 413 = 366 parentless + 47 children |
| Proved / durable canonical | 412 / 412 = 365 parentless + 47 children |
| Native cancellation before execution | 1 PlayCard; retained cancellation |
| Real failures / unresolved / persistence loss | 0 / 0 / 0 |
| Diagnostic invalidations | 44 native-type mismatches: 16 map moves + 28 enemy-turn readiness actions |
| Legacy compatibility subset | 132 valid / 0 invalid; not the Full-Run denominator |
| Native GameAction diagnostic subset | 312 exact + 1 cancelled / 0 unknown |
| Lifecycle | 1 native new-run start to 1 natural defeat, uninterrupted |

All 47 children have earlier canonical parents in the same run and exact causal
root: 26 combat hand selections, 10 card reward choices (including five Skip),
five shop-removal decisions, four rest upgrades and two event reward children.
Each has its own state, catalog, action and successor. Continuation proof on a
parent is distinct from the child's decision occurrence.

All 17 PlayCards accepted while a previous PlayCard was started but unfinished
are canonical. Five potion uses and one noncombat potion discard are canonical.
Polling first observed an active run; the subsequent exact native Launch still
produced its own start. Native terminal preceded durable Recorder Close.

## Transfer and owning repair

Close sealed at 2026-09-12 15:40:30 UTC. The existing service automatically packed
and uploaded; the receiver verified at 15:41:18, and local retry obtained the
matching receipt at 15:41:53 (about 83 seconds after Close). No manual repack,
new enrollment or receipt rewrite was needed. Independent R2 manifest/chunk/hash
readback matched the local archive, transfer manifest, intent and receipt.

The first incorrect operational fact was that an explicit receiver
`verification_pending` response became `TimeoutError`. Evidence rc.5 adds a typed
`ReceiverVerificationPending` outcome: the same durable pending state/backoff and
attempt count remain, but expected verification progress has no transport error.
Actual socket timeouts retain their error. Historical timeout rows cannot be
retrospectively divided into network and verifier waits; they are preserved.

Regression covers pending after restart without another upload, interleaved real
timeout/pending/timeout/verified outcomes, pack-once behavior and unchanged source
files. Existing tool bindings, queue vocabulary and receipts need no migration.
No native gameplay, recorder, catalog, Commit or successor implementation changed.

## Boundaries

STPD projected all 412 records and validated catalogs, lineage and serializers;
its separate Dataset admission correctly stops at one independent run component
(the split implementation needs at least three). That is not recording failure.
No Dataset, model, training job or GPU invocation was produced; compute budget is zero.

This run does not qualify DrawPile/DiscardPile selector ownership, Fruit Juice,
generated/native-origin selectors, multi-act/victory paths, crash recovery,
non-interference, unseen content, every terminal OS or research/model quality.
Combat-pile Reads are not proof that combat-pile selectors occurred. New repair
CI/service receipts prove their exact newer source, not a replayed Human run.
