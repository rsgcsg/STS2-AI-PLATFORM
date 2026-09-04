using System.Text.Json.Nodes;

namespace STS2HumanAnnotator.Core;

/// <summary>
/// The current recording contract. The <c>*-2</c> wire names are retained as
/// evidence identities; they describe one current format rather than a
/// parallel V2 product.
/// </summary>
public static class CurrentRecordingContract
{
    public const string ProductVersion = "0.3.0-rc.1";
    public const int SchemaVersion = 2;
    public const string RecordSchema = "sts2.human-annotator/decision-record-2";
    public const string ManifestSchema = "sts2.human-annotator/recording-manifest-2";
    public const string InvalidationSchema = "sts2.human-annotator/invalidation-2";
    public const string CoverageSchema = "sts2.human-annotator/coverage-2";
    public const string RuntimeStatusSchema = "sts2.human-annotator/runtime-status-2";
    public const string CaptureProfileSchema = "sts2.ai-platform/human-capture-profile-2";
    public const string ReadEvidenceSchema = "sts2.human-annotator/read-evidence-2";
    public const string RunJournalSchema = "sts2.human-annotator/run-journal-event-2";
    public const string SessionBundleSchema = "sts2.human-annotator/session-bundle-2";
    public const string SessionBundleAuditSchema = "sts2.human-annotator/session-bundle-audit-2";
}

public sealed record CaptureReadRequirement(
    string Phase,
    string Kind,
    bool Required,
    string? InteractionKind = null);

public sealed record HumanCaptureProfile(
    int SchemaVersion,
    string Schema,
    string ProfileId,
    string RecordSchema,
    IReadOnlyList<string> SupportedActionFamilies,
    IReadOnlyList<CaptureReadRequirement> Reads,
    IReadOnlyList<string> NonClaims);

public static class FullRunCoverageClassifications
{
    public const string InScopeImplemented = "IN_SCOPE_IMPLEMENTED";
    public const string NotAPlayerDecisionWithNativeJustification =
        "NOT_A_PLAYER_DECISION_WITH_NATIVE_JUSTIFICATION";
    public const string OutOfScopeWithJustification = "OUT_OF_SCOPE_WITH_JUSTIFICATION";
    public const string Blocked = "BLOCKED";
}

/// <summary>
/// Machine-checkable recording coverage map. This is deliberately a map of
/// witness/recording closure, not a legality table or an input authority.
/// </summary>
public sealed record FullRunCoverageEntry(
    string Family,
    string Classification,
    string UiInput,
    string NativeOwner,
    string SemanticProvider,
    string AcceptedSeam,
    string LifecycleCommit,
    string NextAuthoritativeBoundary,
    string? Justification = null);

public static class FullRunCoverageContract
{
    public const string Schema = "sts2.human-annotator/full-run-coverage-1";

    public static IReadOnlyList<FullRunCoverageEntry> Entries { get; } =
        new FullRunCoverageEntry[]
        {
            new("ordinary_combat.play_card", FullRunCoverageClassifications.InScopeImplemented,
                "NCardPlay.TryPlayCard", "NCardPlay", "NativeCombatDecisionProvider",
                "NCardPlay.TryPlayCard", "PlayCardAction lifecycle + native OnPlay commit",
                "next complete interactive combat boundary"),
            new("ordinary_combat.end_turn", FullRunCoverageClassifications.InScopeImplemented,
                "NEndTurnButton.OnRelease", "NEndTurnButton", "NativeCombatDecisionProvider",
                "EndPlayerTurnAction.OnEnqueued", "EndPlayerTurnAction native lifecycle",
                "next complete interactive combat boundary"),
            new("ordinary_combat.use_potion", FullRunCoverageClassifications.InScopeImplemented,
                "NPotionHolder.UsePotion/EnqueueManualUse", "NPotionHolder/PotionModel",
                "NativeCombatDecisionProvider", "UsePotionAction enqueue", "UsePotionAction native lifecycle",
                "next complete interactive combat boundary"),
            new("combat_hand_selector.select", FullRunCoverageClassifications.InScopeImplemented,
                "NPlayerHand.SelectCardIn*Mode", "NPlayerHand", "NativeCombatDecisionProvider",
                "NPlayerHand.SelectCardIn*Mode", "selector confirm/owner callback",
                "next complete interactive combat boundary"),
            new("combat_hand_selector.deselect", FullRunCoverageClassifications.InScopeImplemented,
                "NSelectedHandCardContainer.DeselectHolder", "NSelectedHandCardContainer",
                "NativeCombatDecisionProvider", "NSelectedHandCardContainer.DeselectHolder",
                "selector confirm/owner callback", "next complete interactive combat boundary"),
            new("combat_hand_selector.confirm", FullRunCoverageClassifications.InScopeImplemented,
                "NPlayerHand.OnSelectModeConfirmButtonPressed", "NPlayerHand",
                "NativeCombatDecisionProvider", "NPlayerHand.OnSelectModeConfirmButtonPressed",
                "selector owner completion", "next complete interactive boundary"),
            new("native_generated_card_choice.select", FullRunCoverageClassifications.InScopeImplemented,
                "NChooseACardSelectionScreen.SelectHolder", "NChooseACardSelectionScreen",
                "NativeGeneratedChoice provider", "NChooseACardSelectionScreen.SelectHolder",
                "selector completion + PlayerChoice continuation", "next exact semantic boundary"),
            new("native_generated_card_choice.skip", FullRunCoverageClassifications.InScopeImplemented,
                "NChooseACardSelectionScreen.OnSkipButtonReleased", "NChooseACardSelectionScreen",
                "NativeGeneratedChoice provider", "NChooseACardSelectionScreen.OnSkipButtonReleased",
                "selector completion + PlayerChoice continuation", "next exact semantic boundary"),
            new("boss_relic.select", FullRunCoverageClassifications.InScopeImplemented,
                "NRelicBasicHolder.Released -> NChooseARelicSelection.SelectHolder",
                "NChooseARelicSelection", "NativeBossRelicDecisionProvider",
                "RelicSelectCmd.FromChooseARelicScreen option registration",
                "PlayerChoiceSynchronizer.SyncLocalChoice",
                "RunManager.ActEntered only when a later native act transition occurs; otherwise parent PlayerChoice continuation"),
            new("boss_relic.skip", FullRunCoverageClassifications.InScopeImplemented,
                "NChoiceSelectionSkipButton.Released -> NChooseARelicSelection.OnSkipButtonReleased",
                "NChooseARelicSelection", "NativeBossRelicDecisionProvider",
                "RelicSelectCmd.FromChooseARelicScreen option registration",
                "PlayerChoiceSynchronizer.SyncLocalChoice (empty result)",
                "parent PlayerChoice continuation; no inferred successor"),
            new("map_navigation.travel", FullRunCoverageClassifications.InScopeImplemented,
                "NMapScreen.OnMapPointSelectedLocally", "NMapScreen", "NativeMapDecisionProvider",
                "VoteForMapCoordAction enqueue", "VoteForMapCoordAction native lifecycle",
                "next exact map boundary"),
            new("reward_claim.claim", FullRunCoverageClassifications.InScopeImplemented,
                "NRewardButton.OnRelease", "NRewardButton", "NativeRewardDecisionProvider",
                "NRewardButton.OnRelease", "SelectLocalReward or card-selection owner",
                "next exact reward boundary"),
            new("reward_claim.proceed", FullRunCoverageClassifications.InScopeImplemented,
                "NRewardsScreen.OnProceedButtonPressed", "NRewardsScreen", "NativeRewardDecisionProvider",
                "NRewardsScreen.OnProceedButtonPressed", "RunManager.ProceedFromTerminalRewardsScreen or reward skip",
                "next exact reward/room boundary"),
            new("act_change.ready", FullRunCoverageClassifications.InScopeImplemented,
                "NRewardsScreen.OnProceedButtonPressed terminal boss/victory branch",
                "ActChangeSynchronizer/ActionQueueSynchronizer", "NativeActChangeDecisionProvider",
                "ActChangeSynchronizer.SetLocalPlayerReady -> RequestEnqueue(VoteToMoveToNextActAction)",
                "VoteToMoveToNextActAction.ExecuteAction -> OnPlayerReady",
                "RunManager.ActEntered only after all native readiness votes; no successor is guessed"),
            new("card_reward_selection.select", FullRunCoverageClassifications.InScopeImplemented,
                "NCardRewardSelectionScreen.SelectCard", "NCardRewardSelectionScreen",
                "NativeCardRewardDecisionProvider", "NCardRewardSelectionScreen.SelectCard",
                "card reward owner completion", "next exact reward boundary"),
            new("treasure_room.open", FullRunCoverageClassifications.InScopeImplemented,
                "NTreasureRoom.OnChestButtonReleased", "NTreasureRoom", "NativeTreasureDecisionProvider",
                "NTreasureRoom.OnChestButtonReleased", "OneOffSynchronizer.DoLocalTreasureRoomRewards",
                "next exact treasure/reward boundary"),
            new("treasure_room.select", FullRunCoverageClassifications.InScopeImplemented,
                "NTreasureRoomRelicCollection.PickRelic", "NTreasureRoomRelicCollection",
                "NativeTreasureDecisionProvider", "PickRelicAction enqueue", "PickRelicAction native lifecycle",
                "next exact treasure boundary"),
            new("treasure_room.skip", FullRunCoverageClassifications.InScopeImplemented,
                "NTreasureRoom.OnProceedButtonPressed skip", "NTreasureRoom", "NativeTreasureDecisionProvider",
                "NTreasureRoom.OnProceedButtonPressed", "PickRelicAction/treasure continuation",
                "next exact treasure boundary"),
            new("treasure_room.proceed", FullRunCoverageClassifications.InScopeImplemented,
                "NTreasureRoom.OnProceedButtonPressed proceed", "NTreasureRoom", "NativeTreasureDecisionProvider",
                "NTreasureRoom.OnProceedButtonPressed", "RunManager.ProceedFromTerminalRewardsScreen",
                "next exact terminal boundary"),
            new("event_option.choose", FullRunCoverageClassifications.InScopeImplemented,
                "NEventRoom.OptionButtonClicked", "NEventRoom/EventOption", "NativeRoomDecisionProvider",
                "NEventRoom.OptionButtonClicked -> EventOption.Chosen", "EventOption.Chosen task completion",
                "next exact event boundary"),
            new("event_option.proceed", FullRunCoverageClassifications.InScopeImplemented,
                "NEventRoom.OptionButtonClicked proceed", "NEventRoom/EventOption", "NativeRoomDecisionProvider",
                "NEventRoom.OptionButtonClicked -> EventOption.Chosen", "EventOption.Chosen task completion",
                "next exact event boundary"),
            new("shop_room.open", FullRunCoverageClassifications.InScopeImplemented,
                "NMerchantRoom.OpenInventory", "NMerchantRoom", "NativeRoomDecisionProvider",
                "NMerchantRoom.OpenInventory", "inventory owner open", "next shop inventory boundary"),
            new("shop_room.proceed", FullRunCoverageClassifications.InScopeImplemented,
                "NMerchantRoom.HideScreen", "NMerchantRoom", "NativeRoomDecisionProvider",
                "NMerchantRoom.HideScreen", "shop room handoff", "next exact room boundary"),
            new("shop_inventory.purchase", FullRunCoverageClassifications.InScopeImplemented,
                "MerchantEntry.OnTryPurchaseWrapper", "MerchantEntry/NMerchantInventory",
                "NativeRoomDecisionProvider", "MerchantEntry.OnTryPurchaseWrapper", "purchase task completion",
                "next exact shop boundary"),
            new("shop_inventory.card_removal", FullRunCoverageClassifications.InScopeImplemented,
                "MerchantCardRemovalEntry.OnTryPurchaseWrapper", "MerchantCardRemovalEntry/NMerchantInventory",
                "NativeRoomDecisionProvider", "MerchantEntry.OnTryPurchaseWrapper", "merchant card-removal task opens native selector",
                "nested card selection remains separately BLOCKED; parent purchase is recorded exactly"),
            new("shop_inventory.close", FullRunCoverageClassifications.InScopeImplemented,
                "NMerchantInventory.Close", "NMerchantInventory", "NativeRoomDecisionProvider",
                "NMerchantInventory.Close", "inventory close", "next exact shop boundary"),
            new("rest_site.choose", FullRunCoverageClassifications.InScopeImplemented,
                "NRestSiteButton.OnRelease -> RestSiteSynchronizer.ChooseLocalOption",
                "RestSiteSynchronizer/RestSiteOption", "NativeRoomDecisionProvider",
                "RestSiteSynchronizer.ChooseLocalOption", "ChooseLocalOption task completion",
                "next exact rest-site boundary"),
            new("rest_site.proceed", FullRunCoverageClassifications.InScopeImplemented,
                "NRestSiteRoom.OnProceedButtonReleased", "NRestSiteRoom", "NativeRoomDecisionProvider",
                "NRestSiteRoom.OnProceedButtonReleased", "room handoff", "next exact room boundary"),
            new("run_setup.start", FullRunCoverageClassifications.NotAPlayerDecisionWithNativeJustification,
                "none: RunManager.Launch is native lifecycle setup", "RunManager", "RecorderRuntime",
                "RunManager.Launch", "RunManager.RunStarted event", "RunManager.RoomEntered/first interactive boundary",
                "No player decision is made at this seam."),
            new("run_terminal.end", FullRunCoverageClassifications.NotAPlayerDecisionWithNativeJustification,
                "none: RunManager.OnEnded is native terminal lifecycle", "RunManager", "RecorderRuntime",
                "RunManager.OnEnded(bool)", "RunManager.OnEnded", "no successor; terminal marker only",
                "No player decision is made and no successor is inferred."),
            new("shop_inventory.card_removal_nested_selector", FullRunCoverageClassifications.Blocked,
                "MerchantCardRemovalEntry.OnTryPurchaseWrapper -> OneOffSynchronizer.DoMerchantCardRemoval -> CardSelectCmd.FromDeckForRemoval",
                "NDeckCardSelectScreen", "NativeRoomDecisionProvider (parent only)",
                "BLOCKED: no exact parent registration seam", "CardSelectCmd/NDeckCardSelectScreen child callback has no MerchantCardRemovalEntry carrier",
                "BLOCKED: exact v0.111.0 factory only carries cards+CardSelectorPrefs; ambient/FIFO/latest binding would be guesswork."),
            new("event_option.nested_selector", FullRunCoverageClassifications.Blocked,
                "EventOption.Chosen callback may open a child selector", "NDeckCardSelectScreen or other child owner",
                "NativeRoomDecisionProvider (parent only)", "BLOCKED: EventOption has only a closure callback",
                "EventOption.Chosen completes the parent callback only", "BLOCKED: EventOption exposes no stable child-parent carrier in v0.111.0."),
            new("rest_site.nested_selector", FullRunCoverageClassifications.Blocked,
                "RestSiteOption.OnSelect may call CardSelectCmd.FromDeckForUpgrade", "NDeckCardSelectScreen",
                "NativeRoomDecisionProvider (parent only)", "BLOCKED: RestSiteOption has no public parent carrier",
                "RestSiteSynchronizer.ChooseOption task is parent outcome only", "BLOCKED: exact factory/caller passes cards+prefs and no RestSiteOption identity."),
        };

    public static IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        foreach (FullRunCoverageEntry entry in Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Family)
                || string.IsNullOrWhiteSpace(entry.Classification)
                || string.IsNullOrWhiteSpace(entry.UiInput)
                || string.IsNullOrWhiteSpace(entry.NativeOwner)
                || string.IsNullOrWhiteSpace(entry.SemanticProvider)
                || string.IsNullOrWhiteSpace(entry.AcceptedSeam)
                || string.IsNullOrWhiteSpace(entry.LifecycleCommit)
                || string.IsNullOrWhiteSpace(entry.NextAuthoritativeBoundary))
                errors.Add($"coverage_entry_incomplete:{entry.Family}");
            if (entry.Classification == FullRunCoverageClassifications.Blocked
                && !entry.UiInput.Contains("BLOCKED", StringComparison.Ordinal)
                && !entry.AcceptedSeam.Contains("BLOCKED", StringComparison.Ordinal))
                errors.Add($"blocked_entry_missing_reason:{entry.Family}");
        }
        errors.AddRange(Entries.GroupBy(entry => entry.Family, StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => $"coverage_family_duplicate:{group.Key}"));
        return errors;
    }
}

public sealed record ReadEvidence(
    int SchemaVersion,
    string Schema,
    string ReadEvidenceId,
    string ReadId,
    string Kind,
    string SnapshotId,
    string RuntimeInstanceId,
    string EnvironmentFingerprint,
    string Status,
    string? ContentSchema,
    JsonNode? Completeness,
    string? PayloadRef,
    string? PayloadSha256,
    DateTimeOffset CapturedAt,
    string? ErrorCode,
    string? Detail);

public sealed record CapturedReadPayload(
    string ReadId,
    string Kind,
    string SnapshotId,
    string RuntimeInstanceId,
    string EnvironmentFingerprint,
    string Status,
    string? ContentSchema,
    JsonNode? Content,
    JsonNode? Completeness,
    DateTimeOffset CapturedAt,
    string? ErrorCode,
    string? Detail);

public sealed record CurrentDecisionFrame(
    string SnapshotId,
    string InteractionId,
    string InteractionKind,
    string SurfaceSchema,
    string CatalogDigest,
    int CatalogCount,
    JsonNode Snapshot,
    IReadOnlyList<ReadEvidence> Reads);

public sealed record CurrentSuccessor(
    string SnapshotId,
    string Status,
    string InteractionId,
    string InteractionKind,
    DateTimeOffset ObservedAt,
    JsonNode Snapshot,
    IReadOnlyList<ReadEvidence> Reads);

public sealed record CurrentDecisionRecord(
    int SchemaVersion,
    string Schema,
    string RecordId,
    string SessionId,
    string RunId,
    string TimelineId,
    long Sequence,
    DateTimeOffset RecordedAt,
    RecorderEnvironmentIdentity Environment,
    string CaptureProfileId,
    CurrentDecisionFrame Pre,
    NativeWitnessEvidence NativeWitness,
    ExactMappingEvidence Mapping,
    RecordedBoundAction Action,
    CurrentSuccessor Successor,
    string DecisionFamily,
    string Surface,
    RecordEligibility Eligibility);

public sealed record RunJournalEvent(
    int SchemaVersion,
    string Schema,
    string EventId,
    string SessionId,
    string RunId,
    string TimelineId,
    long Sequence,
    DateTimeOffset RecordedAt,
    string Kind,
    string? RecordId,
    string? SnapshotId,
    string? Detail);

public sealed record CurrentRecordingManifest(
    int SchemaVersion,
    string Schema,
    string SessionId,
    string TimelineId,
    DateTimeOffset CreatedAt,
    string RecorderVersion,
    string RecorderSourceRevision,
    string Platform,
    string CaptureProfileId,
    string CaptureProfileSha256,
    IReadOnlyList<string> SupportedFamilies,
    IReadOnlyList<string> NonClaims);

public sealed record CurrentCoverageSummary(
    int SchemaVersion,
    string Schema,
    string SessionId,
    long AdmittedRecords,
    long Invalidations,
    long ReadMaterialized,
    long ReadFailed,
    IReadOnlyDictionary<string, long> Families,
    IReadOnlyDictionary<string, long> ReadsByKind,
    IReadOnlyDictionary<string, long> InvalidationsByReason,
    DateTimeOffset UpdatedAt);
