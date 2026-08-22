# Exact Native Seam Notes

These findings are bound to local STS2 `v0.111.0`, commit `41cef1ea`, main
assembly SHA-256
`9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4`,
MVID `57785517-0b16-42b9-8b36-bad6fb28384b`.

- `NMouseCardPlay.StartAsync` reaches `NCardPlay.TryPlayCard(target)`.
- `NPlayerHand.StartCardPlay` receives the exact `NHandCardHolder` before the
  holder is moved out of the active hand; this is the last stable pre-action
  freeze point for both mouse and controller card play.
- `NCardPlay.TryPlayCard` uses native checks and `CardModel.TryManualPlay`.
- accepted manual play requests `ActionQueueSynchronizer.RequestEnqueue` with a
  `PlayCardAction`; `NetCombatCard.ToCardModelOrNull()` and `Target` expose the
  exact native operands already accepted by the game.
- `NEndTurnButton.CallReleaseLogic` and `SecretEndTurnLogicViaFtue` request an
  `EndPlayerTurnAction` through the same queue.
- `NCardPlayQueue.OnLocalCardPlayed` removes the accepted holder from
  `NPlayerHand.ActiveHolders`; a Snapshot captured only at `TryPlayCard` can
  therefore be complete yet correctly omit the card being committed.

These are implementation evidence, not current Live evidence. Decompiled source
and proprietary assemblies are not committed.
