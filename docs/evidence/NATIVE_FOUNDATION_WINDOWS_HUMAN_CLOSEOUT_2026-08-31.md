# Native Foundation Windows Human Closeout

Status: `PASS_BOUNDED_NATIVE_FOUNDATION_WINDOWS_HUMAN`

This closeout binds one owner-operated Windows recording to the exact Native
Foundation artifact. It qualifies the bounded PR #5 Combat/PlayerChoice,
cross-domain owner-handoff, and Recorder lifecycle scope. It is not Full-Run
qualification and does not transfer evidence from PR #3 or any predecessor
artifact.

## Exact identity

| Boundary | Exact identity |
| --- | --- |
| STS2 | `v0.111.0 / 41cef1ea` |
| `sts2.dll` SHA-256 / MVID | `0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9` / `73b63ee0-6c0a-47bb-b0d1-b21f6d94222e` |
| Platform implementation source | `a3bcd373e156fb354a6b4947b72c15236457c4b0` |
| Unified artifact SHA-256 / MVID | `a681f8b1b516376a26823114ca42d2dca4c2981c2930e5770872777c3e3bc3a9` / `7c42c4c3-02fb-46c9-ac90-dfb1cf516fdd` |
| Connector protocol / runtime | `1.0.0` / `d8a10ba2a4684182807df332facc881c` |
| Environment fingerprint | `9e0e0cfe9fd7d4cae507059e6588c3cb1ee67f466ccef6d8044269a4c8ee8c7e` |
| Modset status / fingerprint | `exact_platform_modset` / `1f1bdecc945fd4af54d0a5f1296cf6b91d0e82fca180d8b6b3c619bdd52ed135` |
| Ordered loaded Mods | `['STS2_PLATFORM']` |
| Session / timeline | `session-20260831T072650Z-b0608291ae7f416d96b058078f441794` / `timeline-d9b5829a46d54e8cb460dab5d8647a16` |
| Capture profile | `human-combat-read-rich-v2` / `b947eb5ce03ad39a3ce609c738e6667585c20109f8a3ef2a1cadefa87bfdfef2` |
| Safe redeploy rollback | `apps/game-mod/.local/deployments/2026-08-31T07-22-19.739Z` |

The shipped game log for this cold load records one loaded Mod,
`STS2_PLATFORM`; discovered Workshop entries for CombatSolver, RitsuLib, and
CFCRacing were disabled. No Agent, Connector controller, or other automated
gameplay authority was active. The owner performed the run through the shipped
game UI and closed the game after Recorder Close.

## Repository audit

The repository-native auditors ran against the closed session without editing
its files:

- Decision V2 audit: PASS, 35 valid, 0 invalid, 34 explicit invalidations;
- native semantic audit: PASS, 37 accepted roots, 36 successful, one native
  cancellation, zero abort and zero unknown;
- exact semantic membership: 36/36 successful roots exactly once;
- accepted root accounting: 37/37 have exactly one terminal disposition;
- canonical admitted rows: 35, comprising 25 Play, nine End Turn, and one
  targeted potion Use;
- required Reads: 149 materialized, zero failed;
- PlayerChoice lifecycle: three parent pauses and three resumes; one adjacent
  causal handoff explicitly crosses a typed player-choice commit;
- the cancelled End Turn remains `cancelled / not_executed` and is not
  relabelled success.

The exercised Combat catalog includes ordinary and targeted card play, End
Turn, and `Use Powdered Demise -> Shrinker Beetle`. All successful roots retain
coherent native membership; overlap and incomplete-boundary cases fail closed
as invalidations rather than borrowing another Human action's state.

## Cross-domain and Recorder evidence

Within the same session the owner exercised repeated ordered sequences from a
lethal Combat action through Reward claim, CardReward selection/proceed, and
Map vote. The native owner changes from Combat to the appropriate shipped STS2
screen/action type. Direct UI commits are retained as read-only Human evidence;
they do not become Connector legality or delivery authority. Rapid UI commits
that overlap unresolved evidence are explicitly invalidated and do not create
false canonical transitions.

Recorder lifecycle is durably ordered in the RunJournal:

1. New Session at `2026-08-31T07:26:50.4598467Z`;
2. Pause/Resume at `07:27:42.1383168Z` / `07:27:43.5175323Z`;
3. second Pause/Resume at `07:27:45.7416641Z` / `07:28:02.5564426Z`;
4. Close accepted at `07:30:07.2804474Z`;
5. session flushed and closed at `07:30:07.2808416Z`.

Close left no unresolved or unknown native root. The final runtime status was
`recording_closed`, and the game process exited normally afterward.

## Local evidence digests

Raw evidence remains under ignored `.local` storage and is not committed.
These digests bind this closeout to the exact local files:

| File | SHA-256 |
| --- | --- |
| `recording-manifest.json` | `1b12fce39f002fcedafa30e222018361e4fbac827ca89bf9e92ea255b31b175a` |
| `coverage.json` | `9e8d2bf34b2684812b190727a2496800a0c33ca46231f922b0e90c4a7c7091c2` |
| `run-0001.jsonl` | `bc1db402e85652d4150c94537b4131eac9b66326abffc666ebbcb91dd673521e` |
| `run-journal.jsonl` | `0002710bb691bd24513494f52d86b5b62125ce4368680dd12fb2a4051da07852` |
| `native-action-ledger.jsonl` | `eea7e253d29ead94516ba6490593827f70b930ed55c6a24ce07b5a03643ac561` |
| `native-semantic-discriminator.jsonl` | `bea51e9d363fadfdf74cf429f72f998bc7632ea5d19b7d4f658000df30b9e154` |
| `semantic-boundary-trace.jsonl` | `0b48fb2dec30469f1f2ecdb79d530601274bf993f7e2a77b9ccc38c40ee068b5` |
| `canonical-transitions.jsonl` | `fb2021a6bad3a84cc50751cd33dfed6377d2fe70e3cc14772a5a7ab4cfeed86c` |
| `invalidations.jsonl` | `f8606a728cee1ae0c6b1f755104a349d48978446d79dcb4e64dc5b5223bac403` |
| shipped `godot.log` | `d63463f88304800be0e52cc6291ed83dfe063958e7bd4f2b0bd7de78bb5fbefe` |

## Architecture verdict and non-claims

`PASS_BOUNDED_NATIVE_FOUNDATION_WINDOWS_HUMAN`

The exact Windows run validates the shared Native Foundation architecture for
the bounded PR #5 scope: STS2-owned semantics/lifecycle are consumed by both
Connector and Annotator; Connector remains the only BoundAction/delivery
authority; Annotator remains read-only Human evidence; UI does not become
semantic authority; PlayerChoice lineage and the exercised cross-domain owner
handoff remain coherent; Recorder lifecycle closes cleanly.

This evidence does **not** claim exhaustive Full-Run surface coverage, business
settlement from `Receipt.Successor`, canonical training eligibility for every
trace event, headless gameplay parity beyond the separately recorded main-menu
gate, or any Ritsu runtime result. Later docs/audit-only commits do not change
the tested artifact and do not inherit new runtime authority.
