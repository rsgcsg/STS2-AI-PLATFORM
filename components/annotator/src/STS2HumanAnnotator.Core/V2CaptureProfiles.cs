namespace STS2HumanAnnotator.Core;

public static class HumanCaptureProfiles
{
    /// <summary>
    /// The current bounded Full-Run profile. Read requirements are scoped by
    /// interaction so non-combat states do not inherit combat-only reads.
    /// </summary>
    public static HumanCaptureProfile FullRunReadRichV2 { get; } = new(
        HumanRecorderV2Contract.SchemaVersion,
        HumanRecorderV2Contract.CaptureProfileSchema,
        "human-full-run-read-rich-v2",
        HumanRecorderV2Contract.RecordSchema,
        new[]
        {
            "ordinary_combat.play_card",
            "ordinary_combat.end_turn",
            "ordinary_combat.use_potion",
            "native_generated_card_choice.select",
            "native_generated_card_choice.skip",
            "map_navigation.travel",
            "reward_claim.claim",
            "reward_claim.proceed",
            "card_reward_selection.select",
            "treasure_room.open",
            "treasure_room.select",
            "treasure_room.skip",
            "treasure_room.proceed"
        },
        new[]
        {
            new CaptureReadRequirement("pre", "run_deck", true),
            new CaptureReadRequirement("successor", "run_deck", true),
            new CaptureReadRequirement("pre", "combat_piles", true, "combat_turn"),
            new CaptureReadRequirement("successor", "combat_piles", true, "combat_turn"),
            new CaptureReadRequirement("pre", "combat_piles", true, "generated_card_choice"),
            new CaptureReadRequirement("successor", "combat_piles", true, "generated_card_choice")
        },
        new[]
        {
            "not_full_run_journal",
            "potion_target_cancel_before_enqueue_has_no_action_record",
            "selector_live_validation_pending",
            "receipt_is_recording_evidence_not_business_completion",
            "non_combat_successor_requires_exact_native_post_commit_or_execution_handoff"
        });

    // Kept as the historical profile identity for old V2 fixtures and bundles.
    public static HumanCaptureProfile CombatReadRichV2 { get; } = new(
        HumanRecorderV2Contract.SchemaVersion,
        HumanRecorderV2Contract.CaptureProfileSchema,
        "human-combat-read-rich-v2",
        HumanRecorderV2Contract.RecordSchema,
        new[]
        {
            "ordinary_combat.play_card",
            "ordinary_combat.end_turn",
            "ordinary_combat.use_potion",
            "native_generated_card_choice.select",
            "native_generated_card_choice.skip"
        },
        new[]
        {
            new CaptureReadRequirement("pre", "run_deck", true),
            new CaptureReadRequirement("pre", "combat_piles", true),
            new CaptureReadRequirement("successor", "run_deck", true),
            new CaptureReadRequirement("successor", "combat_piles", true)
        },
        new[]
        {
            "not_full_run_journal",
            "potion_target_cancel_before_enqueue_has_no_action_record",
            "selector_live_validation_pending",
            "receipt_is_recording_evidence_not_business_completion"
        });
}
