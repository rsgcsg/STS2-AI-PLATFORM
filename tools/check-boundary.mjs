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
if (!sources.includes("internal static void Postfix([HarmonyArgument(0)] GameAction action)"))
  errors.push("accepted native actions must be observed by a void Postfix");
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

console.log(JSON.stringify({ status: errors.length === 0 ? "pass" : "fail", errors }, null, 2));
if (errors.length) process.exit(1);
