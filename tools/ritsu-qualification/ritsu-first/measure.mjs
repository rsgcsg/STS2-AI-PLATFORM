import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "../../..");
const read = (relative) => fs.readFileSync(path.join(root, relative), "utf8");

function sourceLoc(relative) {
  return read(relative)
    .split(/\r?\n/u)
    .filter((line) => {
      const value = line.trim();
      return value.length > 0
        && !value.startsWith("//")
        && !value.startsWith("///")
        && value !== "{" && value !== "}";
    }).length;
}

function uniqueMentions(relative, values) {
  const source = read(relative);
  return values.filter((value) => source.includes(value));
}

const existingDirectFiles = [
  "components/native-foundation/src/NativeCombatDecisionProvider.cs",
  "components/native-foundation/src/NativeActionLifecycleObserver.cs",
  "components/native-foundation/src/NativeDecisionContracts.cs",
  "components/native-foundation/src/NativeTreasureDecisionProvider.cs",
  "apps/game-mod/NativeFoundationOwnerPatches.cs"
];
const ritsuFirstExistingFiles = [
  "tools/ritsu-qualification/ritsu-first/RitsuFirstCombatAndChoice.cs",
  "tools/ritsu-qualification/ritsu-first/RitsuFirstTreasure.cs"
];
const shopDirect =
  "tools/ritsu-qualification/ritsu-first/ShopDirectExperimentalProvider.cs";
const shopRitsu =
  "tools/ritsu-qualification/ritsu-first/ShopRitsuFirstExperimentalProvider.cs";

const existingTouchpoints = [
  "RunManager.Instance.DebugOnlyGetState",
  "CombatManager.Instance.IsInProgress",
  "PlayerCombatState.Hand.Cards",
  "CanPlayTargeting",
  "PassesCustomUsabilityCheck",
  "ActionQueueSynchronizer.CombatState",
  "CurrentlyRunningAction",
  "BeforeExecuted",
  "BeforePausedForPlayerChoice",
  "BeforeReadyToResumeAfterPlayerChoice",
  "BeforeResumedAfterPlayerChoice",
  "BeforeCancelled",
  "AfterFinished",
  "NTreasureRoom.Create",
  "OnChestButtonReleased",
  "_isRelicCollectionOpen",
  "_hasChestBeenOpened",
  "TreasureRoomRelicSynchronizer.CurrentRelics",
  "GetPlayerVote"
];
const shopTouchpoints = [
  "NMerchantRoom.Instance",
  "DebugOnlyGetState",
  "NMerchantInventory._isInputBlocked",
  "_isInputBlocked",
  "MerchantInventory.AllEntries",
  "AllEntries",
  "MerchantEntry.IsStocked",
  "IsStocked",
  "EnoughGold",
  "HasOpenPotionSlots",
  "Hook.ShouldProcurePotion",
  "ShouldProcurePotion",
  "PurchaseCompleted",
  "PurchaseFailed",
  "ItemPurchasedEvent"
];

function aggregate(files, mentions) {
  return {
    files: files.length,
    loc: files.reduce((sum, file) => sum + sourceLoc(file), 0),
    exact_native_mentions: [...new Set(files.flatMap((file) =>
      uniqueMentions(file, mentions)))].sort()
  };
}

const capability = JSON.parse(read(
  "tools/ritsu-qualification/ritsu-first/capability-decomposition.json"));
const output = {
  schema: "sts2.platform/ritsu-first-measurement-1",
  measured_at: "2026-08-31",
  direct_oracle: "32c76156fed2c14f55427ee88590bf1979598d9d",
  evaluation_base: "4980d268e70c370cc427427d4c71af1dcfe7619e",
  game: {
    version: "0.111.0",
    build: "41cef1ea",
    sts2_sha256: "9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4",
    sts2_mvid: "57785517-0b16-42b9-8b36-bad6fb28384b"
  },
  ritsu: {
    stable: "v0.5.18@f224961a9392e010335da092240b90ee8235317f",
    development: "c466809004f8ecd801956fea2bc3fef83a5d7ad5",
    package_sha256: "03856b26c71bd33a09cd7486d84ee1622cd7bd8a20987648d9350040f575fef3",
    assembly_sha256: "0dc899012a089fac64cb35858840d3263258864e713d3fa23b99dd4cd99cf744",
    assembly_bytes: 8604160
  },
  existing_domains: {
    direct: aggregate(existingDirectFiles, existingTouchpoints),
    ritsu_first: aggregate(ritsuFirstExistingFiles, existingTouchpoints),
    result: {
      complete_catalog_requires_direct_sts2_state_and_validators: true,
      exact_root_disposition_requires_direct_game_action: true,
      player_choice_lineage_requires_direct_action_executor: true,
      treasure_exact_member_names_removed: 0,
      ritsu_categories_genuinely_reused: [
        "typed effect lifecycle",
        "patch registration/startup diagnostics",
        "typed private access"
      ]
    }
  },
  greenfield_shop: {
    direct: aggregate([shopDirect], shopTouchpoints),
    ritsu_first: aggregate([shopRitsu], shopTouchpoints),
    shared_contract_loc: sourceLoc(
      "tools/ritsu-qualification/ritsu-first/RitsuFirstContracts.cs"),
    conformance_fixtures: 3,
    result: {
      same_contract: true,
      same_semantic_catalog: true,
      direct_patch_targets: 0,
      ritsu_first_patch_targets: 0,
      direct_private_members: ["NMerchantInventory._isInputBlocked"],
      ritsu_first_private_members: ["NMerchantInventory._isInputBlocked"],
      direct_lifecycle: [
        "MerchantEntry.PurchaseCompleted",
        "MerchantEntry.PurchaseFailed"
      ],
      ritsu_first_lifecycle: ["ItemPurchasedEvent"],
      ritsu_first_lacks_failed_attempt_disposition: true,
      exact_shop_semantic_members_removed_by_ritsu: 0
    }
  },
  capability_summary: {
    categories: capability.categories.length,
    meaningful_shared_infrastructure: capability.categories
      .filter((entry) => [
        "registration_and_diagnostics_boilerplate",
        "effect_and_room_lifecycle_plumbing",
        "meaningful_shared_infrastructure"
      ].includes(entry.deletion))
      .map((entry) => entry.category),
    syntax_only: capability.categories
      .filter((entry) => entry.syntax_only)
      .map((entry) => entry.category),
    no_deletion: capability.categories
      .filter((entry) => entry.deletion === "none")
      .map((entry) => entry.category)
  },
  version_adaptation: {
    evidence_level: "source_history_no_older_proprietary_assembly_compile",
    supported_api_targets: [
      "0.103.2", "0.106.1", "0.107.0", "0.107.1",
      "0.108.0", "0.109.0", "0.110.0", "0.111.0"
    ],
    update_0_111_commit: "f5b5ad3499e381fafb3d3401bfdf72e02b87deca",
    update_0_111_changed_files: 6,
    update_0_111_sampled_semantic_adapter_files_changed: 0,
    update_0_111_added_version_define_only_for_sampled_contract: true,
    update_0_110_commit: "921f1bea",
    update_0_110_changed_files: 20,
    update_0_110_public_compat_facade_relevant_to_sampled_contract: false,
    platform_escape_hatches_still_version_bound: [
      "Combat logical state and validator signatures",
      "GameAction lifecycle events",
      "ActionExecutor.CurrentlyRunningAction",
      "Treasure private field names",
      "Merchant model and UI resolving members"
    ],
    conclusion: "Ritsu absorbs churn only behind a matching public Ritsu facade; sampled Platform semantics remain direct, while exact package admission is still Platform-owned."
  },
  scorecard: {
    scale: "1 poor to 5 strong for the Platform use case",
    rows: [
      { dimension: "semantic_correctness", direct: 5, ritsu_first: 5 },
      { dimension: "minimal_exact_native_knowledge", direct: 4, ritsu_first: 3 },
      { dimension: "patch_private_burden", direct: 4, ritsu_first: 4 },
      { dimension: "version_condition_burden", direct: 3, ritsu_first: 3 },
      { dimension: "diagnostic_quality", direct: 4, ritsu_first: 5 },
      { dimension: "failure_explicitness", direct: 5, ritsu_first: 5 },
      { dimension: "testability", direct: 4, ritsu_first: 4 },
      { dimension: "greenfield_implementation_effort", direct: 5, ritsu_first: 3 },
      { dimension: "existing_domain_rewrite_clarity", direct: 5, ritsu_first: 3 },
      { dimension: "new_engineer_cognitive_load", direct: 4, ritsu_first: 2 },
      { dimension: "build_headless_integration", direct: 5, ritsu_first: 2 },
      { dimension: "runtime_config_burden", direct: 5, ritsu_first: 2 },
      { dimension: "upstream_supply_risk", direct: 5, ritsu_first: 3 },
      { dimension: "future_version_maintenance", direct: 3, ritsu_first: 3 },
      { dimension: "future_domain_reuse", direct: 4, ritsu_first: 3 }
    ]
  },
  runtime_gate: {
    ritsu_candidate_built: true,
    exact_compile: "pass",
    conformance: "pass",
    installed_or_loaded: false,
    reason: "Mandatory and Selective routes fail value/deletion gates before runtime; loading cannot remove retained exact semantic knowledge or make greenfield Shop thinner."
  }
};

const json = `${JSON.stringify(output, null, 2)}\n`;
if (process.argv.includes("--write")) {
  fs.writeFileSync(path.join(here, "measurement-results.json"), json);
} else {
  process.stdout.write(json);
}
