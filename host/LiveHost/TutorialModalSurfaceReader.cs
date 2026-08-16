using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Ftue;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.addons.mega_text;
using STS2Connector.LiveHost.Contracts;
using STS2Connector.NativeUi;

namespace STS2Connector.LiveHost;

/// <summary>
/// Direct Host binding for the two exact first-run tutorial modals that block
/// ordinary single-player entry on the audited game build. Unknown modal types
/// deliberately do not match this reader and therefore remain fail closed.
/// </summary>
internal sealed class TutorialModalSurfaceReader : ILiveSurfaceReader
{
    internal const string SurfaceKind = "tutorial";
    internal const string AcceptTutorialsId = "accept_tutorials_ftue";
    internal const string CombatRulesId = "combat_rules_ftue";
    internal const string EnableDeliveryEvidence = "native_tutorial_enable_clicked";
    internal const string DisableDeliveryEvidence = "native_tutorial_disable_clicked";
    internal const string PreviousDeliveryEvidence = "native_tutorial_previous_clicked";
    internal const string AdvanceDeliveryEvidence = "native_tutorial_advance_clicked";

    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo? CombatPageField =
        typeof(NCombatRulesFtue).GetField("_currentPage", PrivateInstance);

    public string Kind => SurfaceKind;

    public InputOwnerLayer Layer => InputOwnerLayer.Modal;

    public LiveObservation? TryBuild(
        ActiveSurfaceSnapshot snapshot,
        NativeEntityRegistry entities,
        GameBuildIdentity game) => snapshot.OpenModal switch
        {
            NAcceptTutorialsFtue modal => BuildTutorialPreference(modal, entities, game),
            NCombatRulesFtue modal => BuildCombatRules(modal, entities, game),
            _ => null
        };

    private static LiveObservation BuildTutorialPreference(
        NAcceptTutorialsFtue modal,
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        var context = new MenuLiveContext("menu", "tutorial_preference");
        try
        {
            NVerticalPopup popup = modal.GetNode<NVerticalPopup>("VerticalPopup");
            MegaLabel title = popup.GetNode<MegaLabel>("Header");
            MegaRichTextLabel body = popup.GetNode<MegaRichTextLabel>("Description");
            NPopupYesNoButton yes = popup.GetNode<NPopupYesNoButton>("YesButton");
            NPopupYesNoButton no = popup.GetNode<NPopupYesNoButton>("NoButton");
            var options = new[]
            {
                new VisibleTutorialOption(
                    "enable_tutorials",
                    ReadButtonLabel(yes, "Enable tutorials"),
                    IsUsable(yes)),
                new VisibleTutorialOption(
                    "disable_tutorials",
                    ReadButtonLabel(no, "Disable tutorials"),
                    IsUsable(no))
            }.Where(option => option.Enabled).ToArray();
            var surface = new TutorialSurface(
                SurfaceKind,
                "preference",
                entities.GetId(modal, "screen"),
                AcceptTutorialsId,
                null,
                null,
                ReadLabel(title),
                ReadRichText(body),
                null,
                options);
            return ReadyObservation(game, context, surface, options.Length > 0,
                "NAcceptTutorialsFtue+NVerticalPopup exact current modal and controls");
        }
        catch (Exception ex)
        {
            return BindingUnavailable(
                game,
                context,
                nameof(NAcceptTutorialsFtue),
                $"Tutorial-preference modal binding failed: {ex.GetType().Name}.");
        }
    }

    private static LiveObservation BuildCombatRules(
        NCombatRulesFtue modal,
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        ILiveContext context = LiveContextReader.Build(entities);
        try
        {
            if (CombatPageField?.GetValue(modal) is not int currentPage
                || currentPage is < 1 or > 3)
            {
                return BindingUnavailable(
                    game,
                    context,
                    nameof(NCombatRulesFtue),
                    "The exact combat-tutorial page binding is unavailable or invalid.");
            }

            NButton previous = modal.GetNode<NButton>("LeftArrow");
            NButton next = modal.GetNode<NButton>("RightArrow");
            MegaLabel title = modal.GetNode<MegaLabel>("Header");
            MegaRichTextLabel body = modal.GetNode<MegaRichTextLabel>("%Description");
            MegaLabel pageLabel = modal.GetNode<MegaLabel>("PageCount");
            var options = new List<VisibleTutorialOption>(2);
            if (IsUsable(previous))
            {
                options.Add(new VisibleTutorialOption(
                    "previous_tutorial_page",
                    "Previous page",
                    true));
            }
            if (IsUsable(next))
            {
                options.Add(new VisibleTutorialOption(
                    "advance_tutorial",
                    currentPage == 3 ? "Finish tutorial" : "Next page",
                    true));
            }

            var surface = new TutorialSurface(
                SurfaceKind,
                "page",
                entities.GetId(modal, "screen"),
                CombatRulesId,
                currentPage,
                3,
                ReadLabel(title),
                ReadRichText(body),
                ReadLabel(pageLabel),
                options);
            return ReadyObservation(game, context, surface, options.Count > 0,
                "NCombatRulesFtue exact current modal, _currentPage, and arrow controls");
        }
        catch (Exception ex)
        {
            return BindingUnavailable(
                game,
                context,
                nameof(NCombatRulesFtue),
                $"Combat-tutorial modal binding failed: {ex.GetType().Name}.");
        }
    }

    internal static NativeInputResult StartPreference(
        NativeEntityRegistry entities,
        string expectedScreenId,
        bool enable)
    {
        if (!TryResolveCurrentModal(entities, expectedScreenId, out NAcceptTutorialsFtue? modal)
            || modal == null)
        {
            return NativeInputResult.Rejected(
                "tutorial_preference_owner_changed",
                "The exact tutorial-preference modal is no longer current.");
        }

        try
        {
            NVerticalPopup popup = modal.GetNode<NVerticalPopup>("VerticalPopup");
            NPopupYesNoButton button = popup.GetNode<NPopupYesNoButton>(
                enable ? "YesButton" : "NoButton");
            if (!IsUsable(button))
            {
                return NativeInputResult.Rejected(
                    "tutorial_preference_control_changed",
                    "The advertised tutorial-preference control is no longer visible and enabled.");
            }
            button.ForceClick();
            return NativeInputResult.Delivered(
                enable ? EnableDeliveryEvidence : DisableDeliveryEvidence);
        }
        catch (Exception ex)
        {
            return NativeInputResult.Rejected(
                "tutorial_preference_binding_failed",
                $"The exact tutorial-preference control could not be resolved: {ex.GetType().Name}.");
        }
    }

    internal static NativeInputResult StartCombatPage(
        NativeEntityRegistry entities,
        string expectedScreenId,
        int expectedPage,
        bool advance)
    {
        if (!TryResolveCurrentModal(entities, expectedScreenId, out NCombatRulesFtue? modal)
            || modal == null
            || CombatPageField?.GetValue(modal) is not int currentPage
            || currentPage != expectedPage)
        {
            return NativeInputResult.Rejected(
                "combat_tutorial_page_changed",
                "The exact combat-tutorial owner or page is no longer current.");
        }

        try
        {
            NButton button = modal.GetNode<NButton>(advance ? "RightArrow" : "LeftArrow");
            if (!IsUsable(button))
            {
                return NativeInputResult.Rejected(
                    "combat_tutorial_control_changed",
                    "The advertised combat-tutorial control is no longer visible and enabled.");
            }
            button.ForceClick();
            return NativeInputResult.Delivered(
                advance ? AdvanceDeliveryEvidence : PreviousDeliveryEvidence);
        }
        catch (Exception ex)
        {
            return NativeInputResult.Rejected(
                "combat_tutorial_binding_failed",
                $"The exact combat-tutorial control could not be resolved: {ex.GetType().Name}.");
        }
    }

    private static bool TryResolveCurrentModal<T>(
        NativeEntityRegistry entities,
        string expectedScreenId,
        out T? modal) where T : CanvasItem
    {
        if (entities.TryResolve(expectedScreenId, out modal)
            && modal != null
            && ReferenceEquals(NModalContainer.Instance?.OpenModal, modal)
            && ConnectorMod.IsLiveNode(modal)
            && ConnectorMod.IsNodeVisible(modal))
        {
            return true;
        }
        modal = null;
        return false;
    }

    private static LiveObservation ReadyObservation(
        GameBuildIdentity game,
        ILiveContext context,
        TutorialSurface surface,
        bool hasAction,
        string source)
    {
        string readiness = hasAction ? "ready" : "settling";
        return new LiveObservation(
            StableIdentityHash.Object(new { game.Version, context, surface }),
            readiness,
            context,
            surface,
            new StateCompleteness(
                "contract_complete_for_explicit_exact_tutorial_modal",
                hasAction
                    ? "derived_from_exact_current_visible_enabled_modal_controls"
                    : "temporarily_empty_while_the_exact_modal_controls_mount",
                new[] { source, "NModalContainer.OpenModal exact identity" },
                Array.Empty<string>()),
            game,
            Array.Empty<string>());
    }

    private static LiveObservation BindingUnavailable(
        GameBuildIdentity game,
        ILiveContext context,
        string sourceType,
        string reason) => new(
            StableIdentityHash.Object(new { game.Version, sourceType, reason }),
            "unsupported",
            context,
            new UnsupportedSurface("unsupported", sourceType, reason),
            new StateCompleteness(
                "incomplete_exact_tutorial_modal_binding",
                "empty_fail_closed",
                new[] { "NModalContainer.OpenModal exact identity" },
                new[] { "player_visible_tutorial_semantics", "legal_tutorial_controls" }),
            game,
            new[] { "tutorial_modal_binding_unavailable" })
        {
            InputOwnership = new InputOwnership(
                "none_fail_closed",
                null,
                "The exact tutorial modal could not be projected completely; no action owns input.")
        };

    private static bool IsUsable(NButton button) =>
        ConnectorMod.IsLiveNode(button)
        && ConnectorMod.IsNodeVisible(button)
        && button.IsEnabled;

    private static string ReadButtonLabel(NPopupYesNoButton button, string fallback) =>
        ReadLabel(button.GetNodeOrNull<MegaLabel>("%Label")) ?? fallback;

    private static string? ReadLabel(MegaLabel? label) =>
        string.IsNullOrWhiteSpace(label?.Text) ? null : label.Text.Trim();

    private static string? ReadRichText(MegaRichTextLabel? label)
    {
        if (string.IsNullOrWhiteSpace(label?.Text))
            return null;
        return ConnectorMod.StripRichTextTags(label.Text).Trim();
    }
}
