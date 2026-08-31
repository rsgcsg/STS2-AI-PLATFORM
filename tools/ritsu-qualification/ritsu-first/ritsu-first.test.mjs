import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import test from "node:test";

const root = path.resolve(import.meta.dirname, "../../..");
const read = (relative) => fs.readFileSync(path.join(root, relative), "utf8");
const parse = (relative) => JSON.parse(read(relative));
const folder = "tools/ritsu-qualification/ritsu-first";

const capability = parse(`${folder}/capability-decomposition.json`);
const measurements = parse(`${folder}/measurement-results.json`);
const combat = read(`${folder}/RitsuFirstCombatAndChoice.cs`);
const treasure = read(`${folder}/RitsuFirstTreasure.cs`);
const shopDirect = read(`${folder}/ShopDirectExperimentalProvider.cs`);
const shopRitsu = read(`${folder}/ShopRitsuFirstExperimentalProvider.cs`);
const contracts = read(`${folder}/RitsuFirstContracts.cs`);
const conformance = read(`${folder}/RitsuFirstConformanceProgram.cs`);

test("capability decomposition separates semantics, mechanics, and policy", () => {
  assert.equal(capability.categories.length, 18);
  const byName = new Map(capability.categories.map((entry) => [entry.category, entry]));
  assert.equal(byName.get("semantic_catalog_construction").deletion, "none");
  assert.equal(byName.get("harmony_patch_targets").deletion,
    "registration_and_diagnostics_boilerplate");
  assert.equal(byName.get("private_member_access").syntax_only, true);
  assert.equal(byName.get("runtime_config").deletion, "none");
});

test("Ritsu-first existing-domain providers do not wrap Direct providers", () => {
  assert.match(combat, /class RitsuFirstCombatProvider/u);
  assert.match(combat, /PlayerCombatState\.Hand\.Cards/u);
  assert.match(combat, /CardModel\.CanPlayTargeting/u);
  assert.match(combat, /class RitsuFirstCombatLifecycleProbe/u);
  assert.match(combat, /CurrentlyRunningAction/u);
  assert.doesNotMatch(combat, /NativeCombatDecisionProvider/u);
  assert.doesNotMatch(combat, /NativeActionLifecycleObserver/u);
  assert.doesNotMatch(combat, /NativePlayerChoiceLineage\.Capture/u);

  assert.match(treasure, /class RitsuFirstTreasureProvider/u);
  assert.match(treasure, /PrivateAccess\.FieldRef/u);
  assert.match(treasure, /ApplyRequiredPatcher/u);
  assert.match(treasure, /TreasureRoomRelicSynchronizer/u);
  assert.doesNotMatch(treasure, /NativeTreasureDecisionProvider/u);
});

test("Ritsu effect events remain separate from exact vanilla root lifecycle", () => {
  assert.match(combat, /RitsuLibFramework\.SubscribeLifecycle<CardPlayingEvent>/u);
  assert.match(combat, /HasExactRootAction: false/u);
  assert.match(combat, /_action\.BeforeExecuted/u);
  assert.match(combat, /_action\.BeforeCancelled/u);
  assert.match(combat, /_action\.AfterFinished/u);
  assert.match(combat, /direct STS2 GameAction escape hatch/u);
});

test("greenfield Shop lanes share only the Platform contract", () => {
  assert.match(contracts, /class ExperimentalShopContract/u);
  assert.doesNotMatch(contracts, /MegaCrit\.Sts2/u);
  assert.match(shopDirect, /ExperimentalShopContract\.Project/u);
  assert.match(shopRitsu, /ExperimentalShopContract\.Project/u);
  assert.match(shopDirect, /MerchantInventory\.AllEntries/u);
  assert.match(shopRitsu, /MerchantInventory\.AllEntries/u);
  assert.doesNotMatch(shopRitsu, /ShopDirectExperimentalProvider/u);
  assert.doesNotMatch(shopRitsu, /Native.*DecisionProvider/u);
});

test("Shop conformance covers room, inventory, resolving, and adverse entries", () => {
  assert.match(conformance, /ExperimentalShopStage\.Room/u);
  assert.match(conformance, /ExperimentalShopStage\.Inventory/u);
  assert.match(conformance, /ExperimentalShopStage\.Resolving/u);
  assert.match(conformance, /entry-sold/u);
  assert.match(conformance, /entry-potion/u);
  assert.match(conformance, /entry-removal/u);
  assert.match(conformance, /AssertEquivalent/u);
  assert.equal(measurements.greenfield_shop.conformance_fixtures, 3);
  assert.equal(measurements.greenfield_shop.result.same_semantic_catalog, true);
});

test("Shop comparison records the real lifecycle asymmetry", () => {
  assert.match(shopDirect, /PurchaseCompleted \+=/u);
  assert.match(shopDirect, /PurchaseFailed \+=/u);
  assert.match(shopRitsu, /SubscribeLifecycle<ItemPurchasedEvent>/u);
  assert.doesNotMatch(shopRitsu, /PurchaseFailed/u);
  assert.equal(
    measurements.greenfield_shop.result.ritsu_first_lacks_failed_attempt_disposition,
    true);
  assert.equal(
    measurements.greenfield_shop.result.exact_shop_semantic_members_removed_by_ritsu,
    0);
});

test("measurement output is deterministic and version evidence stays bounded", () => {
  const generated = spawnSync(
    process.execPath,
    [`${folder}/measure.mjs`],
    { cwd: root, encoding: "utf8" });
  assert.equal(generated.status, 0, generated.stderr);
  assert.deepEqual(JSON.parse(generated.stdout), measurements);
  assert.equal(measurements.version_adaptation.evidence_level,
    "source_history_no_older_proprietary_assembly_compile");
  assert.equal(measurements.version_adaptation.update_0_111_sampled_semantic_adapter_files_changed, 0);
});

test("qualification remains isolated from production dependency and behavior", () => {
  const project = read("tools/ritsu-qualification/STS2Platform.RitsuQualification.csproj");
  const rootPackage = parse("package.json");
  assert.match(project, /PackageReference Include="STS2\.RitsuLib"/u);
  assert.match(project, /PrivateAssets="all"/u);
  assert.equal(rootPackage.dependencies?.["STS2.RitsuLib"], undefined);
  assert.equal(rootPackage.devDependencies?.["STS2.RitsuLib"], undefined);
  assert.equal(measurements.runtime_gate.installed_or_loaded, false);
});
