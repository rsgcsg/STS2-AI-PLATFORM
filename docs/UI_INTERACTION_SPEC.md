# Platform UI and interaction specification

This is the canonical presentation vocabulary shared by Workbench and the
in-game Live Workspace. It does not define gameplay legality, BoundAction,
Commit, successor, or Human-evidence authority.

## Status vocabulary

`connected`, `unavailable`, `loading`, `stale`, `recovering`, `error`, and
`tainted` are transport/runtime states. `Human`, `Shadow`, `One-Step`, and
`Auto` are Policy Runtime modes. Recording uses `Ready`, `Recording`,
`Paused`, `Closing`, `Closed`, and `Error` where the authoritative service
provides one. Unknown values render as unavailable and never become an
implicit success.

Human is the safe default. A missing or tainted Policy Runtime disables policy
commands and shows the typed reason; it never fakes readiness or bypasses a
fail-closed boundary.

## Information hierarchy

The small HUD contains only mode, recorder lifecycle, and Connector/Policy
transport health. The K-open Workspace contains separate cards for Overview,
Environment, Policy, Human Data, and Diagnostics. Long identities are shown in
short form with the complete observed value available through the host's normal
copy/expand affordance; values are never inferred from presentation state.

The Recorder card is independent and supports New Session, Pause, Resume, and
Close only when the typed Recording Application status allows them. Every
accepted or rejected command gets a low-noise toast and the next authoritative
poll remains the source of truth.

## Interaction and persistence

Only panels, buttons, drag handles, and resize handles capture pointer input.
The overlay root and HUD are click-through so normal STS2 gameplay input is
unaffected. K toggles the Workspace and Escape closes it. Workspace and
Recorder positions, size, collapse state, and selected page are presentation
state stored in a versioned file under the local application-data directory;
the file is fail-soft, local-only, and never contains secrets, model weights,
raw evidence, or action operands. Invalid or old state returns to defaults.

Drag and resize are clamped to the visible viewport. Reset layout restores the
defaults. Toasts deduplicate by typed key, expire automatically, and can be
dismissed without changing the underlying runtime state.

## Shared copy rules

Use concise labels followed by a typed reason (for example, `Policy Runtime:
unavailable — endpoint not reachable`). Do not describe a loaded artifact as a
Human canary or policy qualification. Connector observation, Policy Runtime
mode/tick commands, and Annotator recording controls are the only application
commands exposed by the UI; gameplay actions remain owned by Connector and the
runtime authority path.
