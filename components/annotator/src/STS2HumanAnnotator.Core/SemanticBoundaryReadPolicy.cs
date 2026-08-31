namespace STS2HumanAnnotator.Core;

/// <summary>
/// Player-reachable Reads required to call a Player Environment frame a
/// complete semantic state. This policy is about information completeness,
/// never action legality or native execution.
/// </summary>
public static class SemanticBoundaryReadPolicy
{
    public static IReadOnlyList<string> CaptureKinds { get; } =
        new[] { "run_deck", "combat_piles", "shop_catalog" };

    public static IReadOnlyList<string> RequiredKinds(string interactionKind)
    {
        if (string.IsNullOrWhiteSpace(interactionKind))
            return Array.Empty<string>();
        if (interactionKind.StartsWith("combat", StringComparison.Ordinal)
            || string.Equals(interactionKind, "generated_card_choice", StringComparison.Ordinal))
        {
            return new[] { "run_deck", "combat_piles" };
        }
        if (string.Equals(interactionKind, "shop_inventory", StringComparison.Ordinal))
            return new[] { "run_deck", "shop_catalog" };
        return new[] { "run_deck" };
    }
}
