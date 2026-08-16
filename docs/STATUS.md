# Status

Release: `0.1.0` evidence preview

Verdict: **H1.0 Core incomplete; Training Ready false; H* unresolved.**

The shipped Godot `--headless` route is retained as the highest-confidence
Reference Host. It has real boot, native control, Read, settling, bounded
journey, isolated reset, capacity and crash/restart evidence. Measured Windows
capacity (`2.91` aggregate normalized decisions/s at 8 workers, about `5.70
GiB` summed peak RSS) rejects it as the current primary trainer.

Completed:

- exact installation discovery and identity;
- exact-build normal-start admission and explicit experimental probe mode;
- released, checksummed Connector setup and rollback integration;
- foreground lifecycle supervision, status, safe PID ownership check, stop,
  local logs and identity record;
- H0/H1/H2 probes with structured local evidence;
- durable rotating trajectory records, canonical fair-player decision
  comparison, and normalized semantic-decision timing;
- separate journey integrity and named-surface coverage verdicts;
- exact-runtime profile templates and destructive reset guarded by template
  payload plus game identity;
- 1/2/4/8 worker capacity and resource measurement;
- fault injection, new-generation restart, distinct-runtime verification,
  process cleanup and endpoint-release checks;
- process-local, secret, runtime-bound native shutdown control outside the
  Player Environment contract;
- public game-independent tests and repository boundary checks.

Current isolation boundary:

- an experimental Windows namespace redirects `HOME`, `USERPROFILE`,
  `APPDATA`, and `LOCALAPPDATA` before process creation and disables Steam
  before platform initialization;
- a fresh real runtime created native SettingsSave v8, prefs, and progress only
  under that namespace while logging that Steam was not initialized;
- native clean template `vanilla-clean` has payload SHA
  `c44a5bb775e650c88e4150dd0a73fe530b6a522df70c1508023505204677b863`;
- one current-artifact fault/restart cycle replaced both profile generation and
  runtime instance, preserved exact environment identity, released process and
  endpoint, and returned no unknown delivery;
- this is still not a release support claim: long soak, Cloud/write sentinels,
  update drill and broader recovery corpus remain absent.

Current blockers:

- game-owned seed/provenance and replay admission;
- semantic differential and first-divergence corpus;
- clean shutdown: native `NGame.Quit()` exits with code 0 and no forced fallback,
  but shipped headless teardown emits roughly 1090 Godot errors at main menu;
- long soak, hang watchdog/fault matrix and game-update requalification drill;
- reproducible RC Host/SDK publication: current local RC evidence cannot be
  reproduced by the stable dependency lock alone;
- a qualified high-throughput backend and real learning/policy-transfer smoke;
- Windows release qualification, Linux, macOS x86_64, later builds and Modsets.

The local exact-build adaptation of `wuhao21/sts2-cli` was rejected as the
primary trainer candidate for now: it reached a decision state only after local
API repairs and then reported profile/bootstrap, CoreCLR patch, localization
and save failures. It remains a useful failure and source-audit corpus, not
semantic parity evidence.
