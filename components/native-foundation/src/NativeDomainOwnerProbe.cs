using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Platform.NativeFoundation;

/// <summary>
/// Distinguishes game-semantic ownership from the native presentation/input
/// owner across major room transitions. This is diagnostic truth only: it does
/// not enumerate or authorize actions.
/// </summary>
public sealed record NativeDomainOwnerObservation(
    string Status,
    string SemanticDomain,
    string InputDomain,
    string? RoomType,
    string? OverlayType,
    bool MapOpen,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> NonClaims);

public static class NativeDomainOwnerProbe
{
    public static NativeDomainOwnerObservation Capture()
    {
        try
        {
            string? roomType = RunManager.Instance.DebugOnlyGetState()?.CurrentRoom?
                .GetType().Name;
            object? overlay = NOverlayStack.Instance?.Peek();
            string? overlayType = overlay?.GetType().Name;
            bool mapOpen = NMapScreen.Instance?.IsOpen == true;
            return Classify(roomType, overlayType, mapOpen);
        }
        catch (Exception exception)
        {
            return new NativeDomainOwnerObservation(
                "unavailable",
                "unknown",
                "unknown",
                null,
                exception.GetType().Name,
                false,
                Array.Empty<string>(),
                new[] { "domain_owner_capture_failed", "no_action_authority" });
        }
    }

    public static NativeDomainOwnerObservation Classify(
        string? roomType,
        string? overlayType,
        bool mapOpen)
    {
        string semantic = overlayType switch
        {
            nameof(NCardRewardSelectionScreen) => "card_reward",
            nameof(NRewardsScreen) => "room_rewards",
            _ when mapOpen => "map_navigation",
            _ when roomType == nameof(TreasureRoom) => "treasure",
            _ when roomType != null => $"room:{roomType}",
            _ => "no_run_domain"
        };
        string input = overlayType switch
        {
            nameof(NCardRewardSelectionScreen) => "card_reward_selection",
            nameof(NRewardsScreen) => "reward_claim",
            _ when mapOpen => "map_navigation",
            _ when roomType == nameof(TreasureRoom) => "treasure_room",
            _ when overlayType != null => $"overlay:{overlayType}",
            _ => "room_or_none"
        };
        bool supportedDiscriminator = semantic is "room_rewards" or "card_reward" or "map_navigation" or "treasure";
        return new NativeDomainOwnerObservation(
            supportedDiscriminator ? "captured" : "observed",
            semantic,
            input,
            roomType,
            overlayType,
            mapOpen,
            new[]
            {
                "RunState.CurrentRoom",
                "NOverlayStack.Peek",
                "NMapScreen.IsOpen"
            },
            new[]
            {
                "owner_discriminator_not_legality",
                "owner_discriminator_not_input_authority",
                "room_transition_completion_not_inferred"
            });
    }
}
