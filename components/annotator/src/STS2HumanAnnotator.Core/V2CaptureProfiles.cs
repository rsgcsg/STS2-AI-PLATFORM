namespace STS2HumanAnnotator.Core;

public static class HumanCaptureProfiles
{
    public static HumanCaptureProfile CombatReadRichV2 { get; } = new(
        HumanRecorderV2Contract.SchemaVersion,
        HumanRecorderV2Contract.CaptureProfileSchema,
        "human-combat-read-rich-v2",
        HumanRecorderV2Contract.RecordSchema,
        new[]
        {
            "ordinary_combat.play_card",
            "ordinary_combat.end_turn",
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
            "potion_use_root_action_not_recorded",
            "selector_live_validation_pending",
            "receipt_is_recording_evidence_not_business_completion"
        });
}
