using STS2Connector.NativeUi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using STS2Connector.LiveHost.Contracts;

namespace STS2Connector.LiveHost;

/// <summary>
/// Exact-build ordinary single-player game-over lifecycle. The intro Continue
/// and the later return button are distinct UI controls; this never
/// invokes a hidden fallback method to skip the current visible stage.
/// </summary>
internal sealed class GameOverSurfaceReader : ILiveSurfaceReader
{
    private const string SurfaceKind = "game_over";
    internal const string AdvanceDeliveryEvidence = "native_game_over_proceed_button_clicked";
    internal const string ReturnDeliveryEvidence = "native_game_over_return_button_clicked";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo? ScoreField = typeof(NGameOverScreen).GetField("_score", Flags);
    private static readonly FieldInfo? AnimatingSummaryField =
        typeof(NGameOverScreen).GetField("_isAnimatingSummary", Flags);

    public string Kind => SurfaceKind;

    public InputOwnerLayer Layer => InputOwnerLayer.Overlay;

    public LiveObservation? TryBuild(
        ActiveSurfaceSnapshot snapshot,
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        if (snapshot.TopOverlay is not NGameOverScreen screen)
            return null;

        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        Player? player = runState == null ? null : LocalContext.GetMe(runState);
        RunHistory? history = RunManager.Instance.History;
        if (runState == null
            || player == null
            || history == null
            || runState.GameMode != GameMode.Standard
            || !ConnectorMod.IsLiveNode(screen)
            || !ConnectorMod.IsNodeVisible(screen)
            || !ActiveScreenContext.Instance.IsCurrent(screen))
        {
            return BindingUnavailable(
                game,
                "Only the exact current ordinary single-player standard game-over screen is supported.");
        }

        NGameOverContinueButton? continueButton =
            screen.GetNodeOrNull<NGameOverContinueButton>("%ContinueButton");
        NReturnToMainMenuButton? mainMenuButton =
            screen.GetNodeOrNull<NReturnToMainMenuButton>("%MainMenuButton");
        NViewRunButton? viewRunButton = screen.GetNodeOrNull<NViewRunButton>("%ViewRunButton");
        NGameOverContinueButton? leaderboardButton =
            screen.GetNodeOrNull<NGameOverContinueButton>("%LeaderboardButton");
        Control? summary = screen.GetNodeOrNull<Control>("%RunSummaryContainer");
        if (continueButton == null
            || mainMenuButton == null
            || viewRunButton == null
            || leaderboardButton == null
            || summary == null
            || ScoreField?.GetValue(screen) is not int score
            || AnimatingSummaryField?.GetValue(screen) is not bool animatingSummary)
        {
            return BindingUnavailable(game, "Exact game-over controls or lifecycle bindings are unavailable.");
        }

        bool continueReady = IsAdvanceable(screen, continueButton);
        bool returnReady = IsActionable(mainMenuButton);
        if (continueReady && returnReady)
            return BindingUnavailable(game, "Game-over intro and return controls are simultaneously actionable.");

        string stage = continueReady
            ? "intro"
            : returnReady
                ? "summary"
                : animatingSummary
                    ? "summary_animating"
                    : "intro_animating";
        string? destination = returnReady
            ? player.DiscoveredEpochs.Count > 0 ? "timeline" : "main_menu"
            : null;
        var context = new GameOverLiveContext(
            "game_over",
            history.Win ? "win" : "loss",
            "standard",
            returnReady ? score : null,
            returnReady ? runState.TotalFloor : null,
            returnReady ? runState.AscensionLevel : null);
        string screenId = entities.GetId(screen, "screen");
        var surface = new GameOverSurface(
            SurfaceKind,
            stage,
            screenId,
            destination,
            continueReady,
            returnReady,
            new[]
            {
                VisibleUnsupportedControl(
                    viewRunButton,
                    entities,
                    "view_run",
                    "View Run",
                    "Run-history navigation is outside the C1 ordinary journey envelope."),
                VisibleUnsupportedControl(
                    leaderboardButton,
                    entities,
                    "leaderboard",
                    "Leaderboard",
                    "Network leaderboard navigation is outside the C1 ordinary journey envelope.")
            }.Where(control => control != null).Cast<VisibleMenuOption>().ToArray());
        bool hasActionableControl = continueReady || returnReady;
        string readiness = hasActionableControl ? "ready" : "settling";
        var completeness = new StateCompleteness(
            "contract_complete_for_ordinary_single_player_game_over_navigation_and_summary",
            hasActionableControl
                ? "derived_from_exact_current_enabled_game_over_controls"
                : "temporarily_empty_while_game_over_intro_or_summary_animation_settles",
            new[]
            {
                "NOverlayStack.Peek+ActiveScreenContext exact input ownership",
                "NGameOverScreen.%ContinueButton+%MainMenuButton+%ViewRunButton+%LeaderboardButton",
                "NGameOverScreen._isAnimatingSummary+%RunSummaryContainer",
                "RunManager.History.Win+RunState.GameMode+TotalFloor+AscensionLevel",
                "NGameOverScreen._score exact-version binding",
                "NGame.ReturnToMainMenuAfterRun+GoToTimelineAfterRun"
            },
            Array.Empty<string>());
        string signature = StableIdentityHash.Object(new
        {
            game.Version,
            context,
            surface
        });
        return new LiveObservation(
            signature,
            readiness,
            context,
            surface,
            completeness,
            game,
            Array.Empty<string>());
    }

    private static bool IsActionable(NButton button) =>
        ConnectorMod.IsLiveNode(button)
        && ConnectorMod.IsNodeVisible(button)
        && button.IsEnabled
        && button.MouseFilter != Control.MouseFilterEnum.Ignore;

    private static bool IsAdvanceable(
        NGameOverScreen screen,
        NGameOverContinueButton button) =>
        IsActionable(button)
        && AnimatingSummaryField?.GetValue(screen) is bool animatingSummary
        && !animatingSummary;

    internal static NativeInputResult StartAdvance(
        NativeEntityRegistry entities,
        string expectedScreenId)
    {
        if (NOverlayStack.Instance?.Peek() is not NGameOverScreen screen
            || !string.Equals(
                entities.GetId(screen, "screen"),
                expectedScreenId,
                StringComparison.Ordinal)
            || screen.GetNodeOrNull<NGameOverContinueButton>("%ContinueButton")
                is not { } continueButton)
        {
            return NativeInputResult.Rejected(
                "game_over_intro_changed",
                "The exact game-over intro screen or Continue control is no longer current.");
        }
        return StartAdvance(screen, continueButton);
    }

    internal static NativeInputResult StartReturn(
        NativeEntityRegistry entities,
        string expectedScreenId)
    {
        if (NOverlayStack.Instance?.Peek() is not NGameOverScreen screen
            || !string.Equals(
                entities.GetId(screen, "screen"),
                expectedScreenId,
                StringComparison.Ordinal)
            || screen.GetNodeOrNull<NReturnToMainMenuButton>("%MainMenuButton")
                is not { } returnButton)
        {
            return NativeInputResult.Rejected(
                "game_over_summary_changed",
                "The exact game-over summary screen or return control is no longer current.");
        }
        return StartReturn(screen, returnButton);
    }

    private static NativeInputResult StartAdvance(
        NGameOverScreen expectedScreen,
        NGameOverContinueButton expectedButton)
    {
        if (NOverlayStack.Instance?.Peek() is not NGameOverScreen current
            || !ReferenceEquals(current, expectedScreen)
            || !ActiveScreenContext.Instance.IsCurrent(expectedScreen)
            || !IsAdvanceable(expectedScreen, expectedButton))
        {
            return NativeInputResult.Rejected(
                "game_over_intro_changed",
                "The game-over intro is no longer the exact current advanceable stage.");
        }

        expectedButton.ForceClick();
        return NativeInputResult.Delivered(AdvanceDeliveryEvidence);
    }

    private static NativeInputResult StartReturn(
        NGameOverScreen expectedScreen,
        NReturnToMainMenuButton expectedButton)
    {
        if (NOverlayStack.Instance?.Peek() is not NGameOverScreen current
            || !ReferenceEquals(current, expectedScreen)
            || !ActiveScreenContext.Instance.IsCurrent(expectedScreen)
            || !IsActionable(expectedButton))
        {
            return NativeInputResult.Rejected(
                "game_over_summary_changed",
                "The game-over summary is no longer the exact current returnable stage.");
        }

        expectedButton.ForceClick();
        return NativeInputResult.Delivered(ReturnDeliveryEvidence);
    }

    private static VisibleMenuOption? VisibleUnsupportedControl(
        NButton button,
        NativeEntityRegistry entities,
        string semanticId,
        string fallbackLabel,
        string blockedReason)
    {
        if (!ConnectorMod.IsNodeVisible(button))
            return null;
        Label? label = ConnectorMod.FindFirst<Label>(button);
        string text = string.IsNullOrWhiteSpace(label?.Text) ? fallbackLabel : label.Text;
        return new VisibleMenuOption(
            entities.GetId(button, "game_over_control"),
            semanticId,
            text,
            null,
            button.IsEnabled,
            "visible_unsupported",
            blockedReason);
    }

    private static LiveObservation BindingUnavailable(GameBuildIdentity game, string detail)
    {
        var context = new UnknownLiveContext("unknown", nameof(NGameOverScreen), detail);
        var surface = new UnsupportedSurface("unsupported", nameof(NGameOverScreen), detail);
        return new LiveObservation(
            StableIdentityHash.Object(new { game.Version, detail }),
            "unsupported",
            context,
            surface,
            new StateCompleteness(
                "incomplete_fail_closed",
