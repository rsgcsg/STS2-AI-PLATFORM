namespace STS2HumanAnnotator.Core;

public static class HumanCaptureProfiles
{
    /// <summary>
    /// The current bounded Full-Run profile. Read requirements are scoped by
    /// interaction so non-combat states do not inherit combat-only reads.
    /// </summary>
    public static HumanCaptureProfile FullRunReadRich { get; } = new(
        CurrentRecordingContract.SchemaVersion,
        CurrentRecordingContract.CaptureProfileSchema,
        "human-full-run-read-rich-v2",
        CurrentRecordingContract.RecordSchema,
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

    // Kept as a bounded current profile identity for existing schema-2 combat
    // fixtures; the CLR path is current and the wire suffix remains evidence
    // identity rather than a parallel runtime product.
    public static HumanCaptureProfile CombatReadRich { get; } = new(
        CurrentRecordingContract.SchemaVersion,
        CurrentRecordingContract.CaptureProfileSchema,
        "human-combat-read-rich-v2",
        CurrentRecordingContract.RecordSchema,
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
