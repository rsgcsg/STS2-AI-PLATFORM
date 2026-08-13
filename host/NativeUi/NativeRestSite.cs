using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Connector.LiveHost;
using STS2Connector.LiveHost.Contracts;

namespace STS2Connector.NativeUi;

/// <summary>
/// Source-free rest-site UI adapter. Player Environment authority comes from
/// the exact visible option buttons, not from option provenance or downstream
/// room effects for REST, SMITH, DIG or future options.
/// </summary>
internal static class NativeRestSite
{
    internal const string SurfaceKind = "rest_site";

    internal static LiveObservation? TryBuild(
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        ActiveSurfaceSnapshot active = ActiveInputResolver.Capture();
        if (active.TopOverlay != null || active.MapIsOpen || active.OpenModal != null)
            return null;

        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        NRestSiteRoom? room = NRestSiteRoom.Instance;
        if (runState?.CurrentRoom is not RestSiteRoom restRoom
            || room == null
            || !ConnectorMod.IsLiveNode(room)
            || CombatManager.Instance.IsInProgress)
        {
            return null;
        }

        RestSiteOption[] options = restRoom.Options.ToArray();
        NRestSiteButton[] buttons = ConnectorMod.FindAll<NRestSiteButton>(room)
            .Where(ConnectorMod.IsLiveNode)
            .ToArray();
        var buttonByOption = new Dictionary<RestSiteOption, NRestSiteButton>();
        foreach (RestSiteOption option in options)
        {
            NRestSiteButton[] matches = buttons
                .Where(button => ReferenceEquals(button.Option, option))
                .ToArray();
            if (matches.Length != 1)
            {
                return BindingUnavailable(
                    game,
                    $"Rest option {option.OptionId} does not have exactly one live UI button.");
            }
            buttonByOption[option] = matches[0];
        }

        string screenId = entities.GetId(room, "screen");
        VisibleRestOption[] visibleOptions = options.Select((option, index) =>
        {
            NRestSiteButton button = buttonByOption[option];
            return new VisibleRestOption(
                entities.GetId(option, "rest_option"),
                index,
                option.OptionId,
                ConnectorMod.SafeGetText(() => option.Title),
                ConnectorMod.SafeGetText(() => option.Description),
                IsOptionActionable(
                    option.IsEnabled,
                    button.IsEnabled,
                    ConnectorMod.IsNodeVisible(button)));
        }).ToArray();
        NProceedButton proceed = room.ProceedButton;
        bool canProceed = proceed.IsEnabled && ConnectorMod.IsNodeVisible(proceed);
        var surface = new RestSiteSurface(
            SurfaceKind,
            screenId,
            visibleOptions,
            canProceed);
        bool actionable = visibleOptions.Any(option => option.Enabled) || canProceed;
        string readiness = actionable ? "ready" : "settling";
        return new LiveObservation(
            StableIdentityHash.Object(new { game.Version, surface, readiness }),
            readiness,
            new RestLiveContext("rest"),
            surface,
            new StateCompleteness(
                "complete_current_structured_ui",
                actionable
                    ? "derived_from_current_visible_enabled_controls"
                    : "temporarily_empty_while_native_ui_settles",
                new[]
                {
                    "RestSiteRoom.Options",
                    "NRestSiteRoom live room",
                    "NRestSiteButton.Option+IsEnabled+visibility",
                    "NRestSiteRoom.ProceedButton"
                },
                Array.Empty<string>()),
            game,
            new[] { "Rest option purpose is visible information, not Player Environment authority." })
        {
            InputOwnership = new InputOwnership(
                "current_ui_owned",
                SurfaceKind,
                "The exact visible rest-site controls own input; upstream business semantics do not grant authority.")
        };
    }

    internal static IReadOnlyList<NativeUiActionDescriptor> DescribeCommands(
        RestSiteSurface surface)
    {
        var commands = new List<NativeUiActionDescriptor>();
        ActionEntityBinding screen = new("screen", surface.ScreenEntityId);
        foreach (VisibleRestOption option in surface.Options.Where(value => value.Enabled))
        {
            commands.Add(new NativeUiActionDescriptor(
                $"rest:option:{surface.ScreenEntityId}:{option.EntityId}",
                "choose_rest_option",
                "selection",
                option.Name ?? option.OptionId,
                "NRestSiteButton current visible enabled UI control",
                new[]
                {
                    screen,
                    new ActionEntityBinding("rest_option", option.EntityId)
                }));
        }
        if (surface.CanProceed)
        {
            commands.Add(new NativeUiActionDescriptor(
                $"rest:proceed:{surface.ScreenEntityId}",
                "proceed_rest_site",
                "navigation",
                "Proceed to map",
                "NRestSiteRoom current visible enabled proceed control",
                new[] { screen }));
        }
        return commands;
    }

    internal static NativeInputResult Start(
        NativeEntityRegistry entities,
        RestSiteSurface surface,
        NativeUiBoundAction binding,
        IReadOnlyDictionary<string, string> parameters)
    {
        string operation = binding.Candidate.Operation;
        if (!parameters.TryGetValue("screen_id", out string? screenId)
            || !string.Equals(screenId, surface.ScreenEntityId, StringComparison.Ordinal))
        {
            return NativeInputResult.Rejected(
                "player_environment_owner_changed",
                "The exact rest-site screen is no longer current.");
        }
        if (operation == "choose_rest_option"
            && parameters.TryGetValue("rest_option_id", out string? optionId))
        {
            return StartOption(entities, screenId, optionId);
        }
        if (operation == "proceed_rest_site")
            return StartProceed(entities, screenId);
        return NativeInputResult.Rejected(
            "player_environment_action_not_current",
            "The requested rest-site affordance is not current.");
    }

    private static NativeInputResult StartOption(
        NativeEntityRegistry entities,
        string expectedScreenId,
        string expectedOptionId)
    {
        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState?.CurrentRoom is not RestSiteRoom restRoom
            || NRestSiteRoom.Instance is not { } room
            || !ConnectorMod.IsLiveNode(room)
            || !string.Equals(entities.GetId(room, "screen"), expectedScreenId, StringComparison.Ordinal)
            || !entities.TryResolve(expectedOptionId, out RestSiteOption? option)
            || option == null)
        {
            return Changed("The exact rest-site screen or option is no longer current.");
        }

        RestSiteOption[] options = restRoom.Options.ToArray();
        NRestSiteButton[] buttons = ConnectorMod.FindAll<NRestSiteButton>(room)
            .Where(button => ConnectorMod.IsLiveNode(button) && ReferenceEquals(button.Option, option))
            .ToArray();
        if (!options.Any(candidate => ReferenceEquals(candidate, option))
            || buttons.Length != 1
            || !IsOptionActionable(option.IsEnabled, buttons[0].IsEnabled, ConnectorMod.IsNodeVisible(buttons[0])))
        {
            return Changed("The exact rest-site option is no longer visible and actionable.");
        }

        buttons[0].ForceClick();
        return NativeInputResult.Delivered("native_rest_option_button_clicked");
    }

    private static NativeInputResult StartProceed(
        NativeEntityRegistry entities,
        string expectedScreenId)
    {
        if (RunManager.Instance.DebugOnlyGetState()?.CurrentRoom is not RestSiteRoom
            || NRestSiteRoom.Instance is not { } room
            || !ConnectorMod.IsLiveNode(room)
            || !string.Equals(entities.GetId(room, "screen"), expectedScreenId, StringComparison.Ordinal)
            || !room.ProceedButton.IsEnabled
            || !ConnectorMod.IsNodeVisible(room.ProceedButton))
        {
            return Changed("The exact rest-site proceed control is no longer visible and actionable.");
        }

        room.ProceedButton.ForceClick();
        return NativeInputResult.Delivered("native_rest_proceed_button_clicked");
    }

    internal static bool IsOptionActionable(
        bool optionEnabled,
        bool buttonEnabled,
        bool buttonVisible) => optionEnabled && buttonEnabled && buttonVisible;

    private static NativeInputResult Changed(string detail) =>
        NativeInputResult.Rejected("player_environment_target_changed", detail);

    private static LiveObservation BindingUnavailable(
        GameBuildIdentity game,
        string reason)
    {
        var context = new RestLiveContext("rest");
        return NativeUiFailClosedObservation.BindingUnavailable(
            game,
            context,
            nameof(NRestSiteRoom),
            reason,
            new[] { "NRestSiteRoom current UI mechanics" },
            new[] { "visible_rest