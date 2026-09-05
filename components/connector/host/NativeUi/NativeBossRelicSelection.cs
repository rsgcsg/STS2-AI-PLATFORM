using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using STS2Connector.LiveHost;
using STS2Connector.LiveHost.Contracts;
using STS2Platform.NativeFoundation;

namespace STS2Connector.NativeUi;

/// <summary>
/// Exact presentation adapter for STS2's boss/out-of-combat relic choice.
/// The parent PlayerChoice and its Commit remain Native Foundation facts; this
/// adapter only binds the visible holders and skip control.
/// </summary>
internal static class NativeBossRelicSelection
{
    internal const string SurfaceKind = "native_boss_relic_selection";
    internal const string SelectOperation = "select_boss_relic";
    internal const string SkipOperation = "skip_boss_relic";

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo? ScreenCompleteField =
        typeof(NChooseARelicSelection).GetField("_screenComplete", Flags);
    private static readonly FieldInfo? RelicsField =
        typeof(NChooseARelicSelection).GetField("_relics", Flags);

    internal static LiveObservation? TryBuild(
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        ActiveSurfaceSnapshot active = ActiveInputResolver.Capture();
        if (active.TopOverlay is not NChooseARelicSelection screen)
            return null;

        ILiveContext context = LiveContextReader.Build(entities);
        if (ScreenCompleteField?.GetValue(screen) is not bool screenComplete)
        {
            return NativeUiFailClosedObservation.BindingUnavailable(
                game,
                context,
                nameof(NChooseARelicSelection),
                "The exact NChooseARelicSelection._screenComplete binding is unavailable.",
                new[] { "NChooseARelicSelection exact-version UI binding" },
                new[] { "relics", "screen_completion" },
                "player_environment_boss_relic_binding_unavailable",
                "player-environment.boss-relic.binding-unavailable",
                "The current relic choice cannot be represented without guessing completion.");
        }

        Control? row = screen.GetNodeOrNull<Control>("RelicRow");
        NChoiceSelectionSkipButton? skip = screen.GetNodeOrNull<NChoiceSelectionSkipButton>("SkipButton");
        if (row == null || skip == null)
        {
            return NativeUiFailClosedObservation.BindingUnavailable(
                game,
                context,
                nameof(NChooseARelicSelection),
                "The exact relic row or skip control is unavailable.",
                new[] { "NChooseARelicSelection structured controls" },
                new[] { "visible_relics", "current_controls" },
                "player_environment_boss_relic_controls_unavailable",
                "player-environment.boss-relic.controls-unavailable",
                "The current relic choice cannot be represented without guessing a target.");
        }

        if (RelicsField?.GetValue(screen) is not IReadOnlyList<RelicModel> nativeRelics)
        {
            return NativeUiFailClosedObservation.BindingUnavailable(
                game,
                context,
                nameof(NChooseARelicSelection),
                "The exact NChooseARelicSelection._relics command option list is unavailable.",
                new[] { "RelicSelectCmd.FromChooseARelicScreen.relics" },
                new[] { "native_boss_relic_options", "parent_lineage" },
                "player_environment_boss_relic_options_unavailable",
                "player-environment.boss-relic.options-unavailable",
                "The current relic choice cannot be represented without its native command option list.");
        }

        NativeBossRelicDecision decision =
            NativeBossRelicDecisionProvider.Capture(screen, nativeRelics, entities);
        if (decision.Status != "captured")
        {
            return NativeUiFailClosedObservation.BindingUnavailable(
                game,
                context,
                nameof(NChooseARelicSelection),
                decision.Detail ?? "The native boss relic semantic owner is unavailable.",
                new[] { NativeBossRelicDecisionProvider.ParentCommand },
                new[] { "native_boss_relic_decision" },
                "player_environment_boss_relic_semantics_unavailable",
                "player-environment.boss-relic.semantics-unavailable",
                "The current relic choice cannot be represented without guessing its owner.");
        }

        NRelicBasicHolder[] holders = row.GetChildren()
            .OfType<NRelicBasicHolder>()
            .Where(holder => ConnectorMod.IsNodeVisible(holder) && holder.Relic?.Model != null)
            .OrderBy(holder => holder.Position.X)
            .ThenBy(holder => holder.Position.Y)
            .ToArray();
        RelicModel[] visibleRelics = holders
            .Select(holder => holder.Relic.Model)
            .ToArray();
        if (holders.Length == 0
            || !NativeDecisionProjection.HasExactReferenceBijection(nativeRelics, visibleRelics))
        {
            return NativeUiFailClosedObservation.BindingUnavailable(
                game,
                context,
                nameof(NChooseARelicSelection),
                "The visible relic holders do not form an exact one-to-one native set.",
                new[] { "NChooseARelicSelection visible relic holders" },
                new[] { "visible_relics" },
                "player_environment_boss_relic_referents_unavailable",
                "player-environment.boss-relic.referents-unavailable",
                "The current relic choice cannot be represented without guessing a target.");
        }

        string screenId = entities.GetId(screen, "screen");
        VisibleRelic[] projectedRelics = decision.Relics
            .Select(relic => VisibleEntityFacts.BuildRelic(relic, entities))
            .ToArray();
        bool controlsReady = !screenComplete &&
                             holders.All(holder =>
                                 holder.IsEnabled
                                 && holder.MouseFilter != Control.MouseFilterEnum.Ignore) &&
                             !screen.IsQueuedForDeletion();
        string[] selectable = controlsReady
            ? projectedRelics.Select(relic => relic.EntityId).OrderBy(id => id, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();
        bool canSkip = decision.SkipPathProven
                       && controlsReady
                       && skip.IsEnabled
                       && skip.MouseFilter != Control.MouseFilterEnum.Ignore
                       && ConnectorMod.IsNodeVisible(skip);
        var surface = new NativeBossRelicSelectionSurface(
            SurfaceKind,
            screenId,
            projectedRelics,
            selectable,
            canSkip);
        bool actionable = selectable.Length > 0 || canSkip;
        return new LiveObservation(
            StableIdentityHash.Object(new { game.Version, surface, decision.ParentLineage.Status }),
            actionable ? "ready" : "settling",
            context,
            surface,
            new StateCompleteness(
                controlsReady ? "complete_current_structured_ui" : "partial_current_structured_ui",
                actionable
                    ? "derived_from_current_visible_enabled_relic_controls"
                    : "temporarily_empty_while_native_relic_choice_settles",
                new[]
                {
                    "NChooseARelicSelection visible NRelicBasicHolder set",
                    "NChooseARelicSelection skip control",
                    $"NativePlayerChoiceLineage:{decision.ParentLineage.Status}",
                    NativeBossRelicDecisionProvider.CommitSeam
                },
                controlsReady ? Array.Empty<string>() : new[] { "current_relic_controls" }),
            game,
            new[]
            {
                "RelicSelectCmd parent continuation remains native; this adapter does not infer a later room or act.",
                $"NativePlayerChoiceLineage:{decision.ParentLineage.Status}"
            })
        {
            InputOwnership = new InputOwnership(
                "current_ui_owned",
                SurfaceKind,
                "The exact NChooseARelicSelection controls own current input.")
        };
    }

    internal static IReadOnlyList<NativeUiActionDescriptor> DescribeCommands(
        NativeBossRelicSelectionSurface surface)
    {
        if (surface.Kind != SurfaceKind)
            return Array.Empty<NativeUiActionDescriptor>();

        ActionEntityBinding screen = new("screen", surface.ScreenEntityId);
        HashSet<string> selectable = surface.SelectableRelicEntityIds.ToHashSet(StringComparer.Ordinal);
        var actions = surface.Relics
            .Where(relic => selectable.Contains(relic.EntityId))
            .Select(relic => new NativeUiActionDescriptor(
                $"{SelectOperation}:{surface.ScreenEntityId}:{relic.EntityId}",
                SelectOperation,
                "selection",
                $"Choose {relic.Name ?? relic.DefinitionId}",
                $"NChooseARelicSelection.SelectHolder+{NativeBossRelicDecisionProvider.ParentCommand}",
                new[]
                {
                    screen,
                    new ActionEntityBinding("relic", relic.EntityId)
                }))
            .ToList();
        if (surface.CanSkip)
        {
            actions.Add(new NativeUiActionDescriptor(
                $"{SkipOperation}:{surface.ScreenEntityId}",
                SkipOperation,
                "alternative",
                "Skip",
                "NChooseARelicSelection.OnSkipButtonReleased",
                new[] { screen }));
        }
        return actions;
    }

    internal static NativeInputResult Start(
        NativeEntityRegistry entities,
        NativeBossRelicSelectionSurface surface,
        NativeUiBoundAction binding,
        IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("screen_id", out string? screenId)
            || !string.Equals(screenId, surface.ScreenEntityId, StringComparison.Ordinal))
        {
            return Changed("The exact boss relic screen is no longer current.");
        }
        if (binding.Candidate.Operation == SelectOperation
            && parameters.TryGetValue("choice_id", out string? relicId))
            return StartSelect(entities, screenId, relicId);
        if (binding.Candidate.Operation == SkipOperation)
            return StartSkip(entities, screenId);
        return Changed("The requested boss relic affordance is not current.");
    }

    private static NativeInputResult StartSelect(
        NativeEntityRegistry entities,
        string expectedScreenId,
        string expectedRelicId)
    {
        if (!TryCurrent(
                entities,
                expectedScreenId,
                out NChooseARelicSelection? screen,
                out NRelicBasicHolder[] holders,
                out IReadOnlyList<RelicModel>? nativeRelics)
            || !entities.TryResolve(expectedRelicId, out RelicModel? relic)
            || relic == null)
        {
            return Changed("The exact boss relic screen or relic is no longer current.");
        }

        if (!NativeBossRelicDecisionProvider.ValidateCurrentExecution(
                nativeRelics!,
                relic,
                requireSkip: false,
                out _))
            return Changed("The exact native boss relic option or PlayerChoice parent is no longer current.");

        NRelicBasicHolder[] matches = holders
            .Where(holder => ReferenceEquals(holder.Relic.Model, relic))
            .ToArray();
        if (matches.Length != 1
            || !matches[0].IsEnabled
            || matches[0].MouseFilter == Control.MouseFilterEnum.Ignore)
            return Changed("The advertised boss relic is no longer visible and enabled.");

        matches[0].ForceClick();
        return NativeInputResult.Delivered("native_boss_relic_holder_clicked");
    }

    private static NativeInputResult StartSkip(
        NativeEntityRegistry entities,
        string expectedScreenId)
    {
        if (!TryCurrent(
                entities,
                expectedScreenId,
                out NChooseARelicSelection? screen,
                out _,
                out IReadOnlyList<RelicModel>? nativeRelics)
            || screen == null
            || nativeRelics == null
            || !NativeBossRelicDecisionProvider.ValidateCurrentExecution(
                nativeRelics,
                expectedRelic: null,
                requireSkip: true,
                out _))
        {
            return Changed("The advertised boss relic skip control is no longer current.");
        }

        NChoiceSelectionSkipButton? skip =
            screen.GetNodeOrNull<NChoiceSelectionSkipButton>("SkipButton");
        if (skip == null
            || !skip.IsEnabled
            || skip.MouseFilter == Control.MouseFilterEnum.Ignore
            || !ConnectorMod.IsNodeVisible(skip))
            return Changed("The advertised boss relic skip control is no longer current.");

        skip.ForceClick();
        return NativeInputResult.Delivered("native_boss_relic_skip_clicked");
    }

    private static bool TryCurrent(
        NativeEntityRegistry entities,
        string expectedScreenId,
        out NChooseARelicSelection? screen,
        out NRelicBasicHolder[] holders,
        out IReadOnlyList<RelicModel>? nativeRelics)
    {
        screen = null;
        holders = Array.Empty<NRelicBasicHolder>();
        nativeRelics = null;
        if (!entities.TryResolve(expectedScreenId, out NChooseARelicSelection? resolved)
            || resolved == null
            || !ActiveInputResolver.IsVisibleActiveOverlay(resolved)
            || !ReferenceEquals(NOverlayStack.Instance?.Peek(), resolved)
            || ScreenCompleteField?.GetValue(resolved) is not false)
            return false;

        if (RelicsField?.GetValue(resolved) is not IReadOnlyList<RelicModel> resolvedRelics)
            return false;
        Control? row = resolved.GetNodeOrNull<Control>("RelicRow");
        if (row == null)
            return false;
        holders = row.GetChildren()
            .OfType<NRelicBasicHolder>()
            .Where(holder => ConnectorMod.IsNodeVisible(holder) && holder.Relic?.Model != null)
            .ToArray();
        if (holders.Length == 0
            || !NativeDecisionProjection.HasExactReferenceBijection(
                resolvedRelics,
                holders.Select(holder => holder.Relic.Model).ToArray()))
            return false;
        screen = resolved;
        nativeRelics = resolvedRelics;
        return true;
    }

    private static NativeInputResult Changed(string detail) =>
        NativeInputResult.Rejected("player_environment_target_changed", detail);
}
