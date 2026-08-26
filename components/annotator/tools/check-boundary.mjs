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
if (!sources.includes("PlayerEnvironmentNativeWitness.Capture()"))
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
if (!recorderRuntime.includes("|| HasPendingRecordingWorkUnsafe())"))
  errors.push("close must wait for strict candidates and unresolved native lifecycle witnesses");
if (!recorderRuntime.includes("NativeActionLedger.CanAdmitStrictTransition"))
  errors.push("strict V2 settlement must require exact native terminal lifecycle");
if (!recorderRuntime.includes("displaced != null && displaced.NativeActionWitnessId == null"))
  errors.push("a missing prior pending action must not be treated as an overlapping UI causal window");
if (recorderRuntime.includes("overlapping_action_before_successor"))
  errors.push("overlap must be accounted in the native ledger rather than dropped");

console.log(JSON.stringify({ status: errors.length === 0 ? "pass" : "fail", errors }, null, 2));
if (errors.length) process.exit(1);
