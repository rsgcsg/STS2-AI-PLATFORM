import fs from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const sourceDirs = [
  path.join(root, "src", "STS2HumanAnnotator.Mod"),
  path.join(root, "src", "STS2HumanAnnotator.Core")
];
const sources = sourceDirs.flatMap((directory) => fs.readdirSync(directory)
  .filter((name) => name.endsWith(".cs"))
  .map((name) => fs.readFileSync(path.join(directory, name), "utf8")))
  .join("\n");
const applicationService = fs.readFileSync(
  path.join(root, "src", "STS2HumanAnnotator.Mod", "RecordingApplicationService.cs"),
  "utf8"
);
const recorderRuntime = fs.readFileSync(
  path.join(root, "src", "STS2HumanAnnotator.Mod", "RecorderRuntime.cs"),
  "utf8"
);
const nativeUiPatches = fs.readFileSync(
  path.join(root, "src", "STS2HumanAnnotator.Mod", "NativeUiPatches.cs"),
  "utf8"
);

const forbidden = [
  ["HarmonyTranspiler", "transpiler patches are forbidden"],
  ["Input.ParseInputEvent", "synthetic coordinate/input injection is forbidden"],
  ["MousePosition", "mouse coordinates cannot identify actions"],
  ["SourceContract", "business source authority is forbidden"],
  ["Capabilities.ExecutionAvailable", "recording cannot depend on Connector mutation permission"],
  ["PlayerEnvironmentService.Submit", "the recorder cannot execute Connector actions"],
  ["RequestEnqueue(new", "the recorder cannot enqueue game actions"],
  ["_latestAuthoritativeFrame", "a generic latest-frame fallback cannot authorize a human action"]
];
const errors = [];
for (const [needle, detail] of forbidden) {
  if (sources.includes(needle)) errors.push(detail);
}
if (!sources.includes("[HarmonyPatch(typeof(GameAction), nameof(GameAction.OnEnqueued))]"))
  errors.push("accepted native actions must be observed after exact GameAction enqueue");
if (!sources.includes("NativeActionLifecycleSubscription"))
  errors.push("accepted native actions require a bounded process-local lifecycle witness");
if (!sources.includes("PlayerEnvironmentNativeWitness.Capture("))
  errors.push("the recorder must consume the process-local Connector witness");
if (!sources.includes("reference_equality_to_frozen_host_binding"))
  errors.push("the record gate must require exact frozen reference mapping");
if (!sources.includes("StageCardPlay(CardModel card)"))
  errors.push("card play must stage its exact pre-action frame before native hand removal");
if (!sources.includes("ReferenceEquals(staged.Card, stagedCard)"))
  errors.push("staged card frames must remain bound to the exact native card reference");
if (!sources.includes("context.AcceptsRootAction(nativeActionType)"))
  errors.push("same-type game actions must not claim the human root before exact mapping");
if (!sources.includes("if (!IsExact(match) || !context.TryClaimRootAction(nativeActionType))"))
  errors.push("the recorder must exact-match before claiming the human root action");
if (!applicationService.includes("RecordingCommandResult Execute(RecordingCommand command)"))
  errors.push("all recording views must use the typed RecordingService command boundary");
if (!applicationService.includes("RecordingEventBatch QueryEvents(long afterSequence)"))
  errors.push("recording views require the typed reconnectable event query boundary");
if (/HumanActionScope|GameAction|AppendDecision|PlayerEnvironmentService/u.test(applicationService))
  errors.push("application commands must not enter native witness or gameplay execution paths");
if (!recorderRuntime.includes("RecordingLifecycleSnapshot.Ready(now)"))
  errors.push("runtime initialization must stop at Ready without creating a session");
if (!recorderRuntime.includes("RecordingCommandKind.StartNewSession"))
  errors.push("recording session creation must be an explicit typed command");
if (!recorderRuntime.includes("TerminateClosePendingWork()"))
  errors.push("user Close must terminate pending work at the recording boundary");
if (!recorderRuntime.includes("NativeActionLedger.CanAdmitStrictTransition"))
  errors.push("strict V2 settlement must require exact native terminal lifecycle");
if (!recorderRuntime.includes("displaced != null && displaced.NativeActionWitnessId == null"))
  errors.push("a missing prior pending action must not be treated as an overlapping UI causal window");
if (recorderRuntime.includes("overlapping_action_before_successor"))
  errors.push("overlap must be accounted in the native ledger rather than dropped");
if (!recorderRuntime.includes("SerializedEvidenceAdmission.Evaluate"))
  errors.push("canonical Human collection must use the one-strict-evidence-window admission policy");
if (!recorderRuntime.includes("TrySettle(pending, frame)"))
  errors.push("the next input boundary must reuse one authoritative frame for predecessor settlement");
if (recorderRuntime.includes("TryObserveSemanticDecisionBoundary();"))
  errors.push("semantic successor collection must not poll a complete Snapshot every process frame");
if (recorderRuntime.includes("ObserveSemanticAccepted(pending")
    || recorderRuntime.includes("ObserveSemanticUiAction(pending")
    || recorderRuntime.includes("else\n            ObserveSemanticLifecycle(subscription, kind);"))
  errors.push("canonical mutations must not feed the legacy semantic tracker in parallel");
if (!recorderRuntime.includes("if (!BoundaryTracker.HasUnresolvedActions)"))
  errors.push("legacy semantic boundary materialization requires real tracker debt");
if (recorderRuntime.includes("_serializedCloseBoundaryRequested")
    || recorderRuntime.includes("_semanticCloseDrainDeadline")
    || recorderRuntime.includes("recording_close_drain_timeout"))
  errors.push("user Close must not wait on a semantic drain deadline or report a timeout");
if (!recorderRuntime.includes("RecordingClosePolicy.TerminalUnknownReason"))
  errors.push("user Close must preserve an explicit terminal successor-unknown reason");
if (/\b(?:internal|private)\s+static\s+bool\s+Prefix\s*\(/u.test(nativeUiPatches))
  errors.push("annotator Prefixes must never skip a native STS2 method");
if (/AllowMutation|BlockMutation/u.test(sources))
  errors.push("evidence admission must not create gameplay mutation authority");
if (!nativeUiPatches.includes("RecorderRuntime.StageCardPlay(card);"))
  errors.push("card staging must observe the exact pre-action frame without controlling native input");
if (!recorderRuntime.includes("native input continues without strict transition evidence"))
  errors.push("unresolved evidence must fail closed without blocking native Human input");

console.log(JSON.stringify({ status: errors.length === 0 ? "pass" : "fail", errors }, null, 2));
if (errors.length) process.exit(1);
