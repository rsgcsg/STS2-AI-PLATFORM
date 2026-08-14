using STS2Connector.NativeUi;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using STS2Connector.LiveHost.Contracts;

namespace STS2Connector.LiveHost;

/// <summary>
/// Exact two-stage protocol for choosing one visible pack of cards. A bundle
/// is one atomic reward choice; its cards are not independent actions.
/// </summary>
internal sealed class CardBundleSelectionSurfaceReader : ILiveSurfaceReader
{
    private const string SurfaceKind = "card_bundle_selection";
    internal const string PreviewDeliveryEvidence = "native_bundle_hitbox_clicked";
    internal const string ConfirmDeliveryEvidence = "native_bundle_confirm_clicked";
    internal const string CancelPreviewDeliveryEvidence = "native_bundle_cancel_clicked";

    public string Kind => SurfaceKind;

    public InputOwnerLayer Layer => InputOwnerLayer.Overlay;

    public LiveObservation? TryBuild(
        ActiveSurfaceSnapshot snapshot,
        NativeEntityRegistry entities,
        GameBuildIdentity game)
    {
        if (snapshot.TopOverlay is not NChooseABundleSelectionScreen screen)
            return null;

        Control? row = screen.GetNodeOrNull<Control>("%BundleRow");
        Control? preview = screen.GetNodeOrNull<Control>("%BundlePreviewContainer");
        Control? previewCards = screen.GetNodeOrNull<Control>("%Cards");
        NBackButton? cancel = screen.GetNodeOrNull<NBackButton>("%Cancel");
        NConfirmButton? confirm = screen.GetNodeOrNull<NConfirmButton>("%Confirm");
        NCommonBanner? banner = screen.GetNodeOrNull<NCommonBanner>("Banner");
        if (row == null || preview == null || previewCards == null || cancel == null || confirm == null || banner?.label == null)
            return BindingUnavailable(game, LiveContextReader.Build(entities), "Visible card-bundle controls are unavailable.");

        NCardBundle[] allBundles = ConnectorMod.FindAll<NCardBundle>(screen)
            .Where(ConnectorMod.IsLiveNode)
            .OrderBy(bundle => bundle.Position.X)
            .ThenBy(bundle => bundle.Position.Y)
            .ToArray();
        if (allBundles.Length == 0 || allBundles.Any(bundle => bundle.Bundle == null || bundle.Bundle.Count == 0))
            return BindingUnavailable(game, LiveContextReader.Build(entities), "No complete visible card bundles are bound.");
        bool previewShowing = preview.Visible;
        NCardBundle? selected = previewShowing ? ResolvePreviewedBundle(screen, allBundles) : null;
        if (previewShowing && selected == null)
            return BindingUnavailable(game, LiveContextReader.Build(entities), "The preview cards do not identify exactly one visible bundle.");

        string? prompt = ReadText(banner.label);
        NCardBundle[] exposedBundles = selected == null ? allBundles : new[] { selected };
        VisibleCardBundle[] bundles = exposedBundles.Select(bundle => new VisibleCardBundle(
            entities.GetId(bundle, "card_bundle"),
            bundle.Bundle.Select(card => LiveContextReader.BuildCard(
                card,
                entities.GetId(card, "card"),
                displayPile: PileType.None)).ToArray())).ToArray();
        string? selectedId = selected == null ? null : entities.GetId(selected, "card_bundle");
        string stage = previewShowing ? "preview" : "choosing";
        string[] selectableBundleIds = !previewShowing && row.Visible
            ? allBundles.Where(bundle =>
                    ConnectorMod.IsNodeVisible(bundle) && bundle.Hitbox is { IsEnabled: true })
                .Select(bundle => entities.GetId(bundle, "card_bundle"))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();
        bool canConfirm = previewShowing
                          && confirm.IsEnabled
                          && ConnectorMod.IsNodeVisible(confirm);
        bool canCancelPreview = previewShowing
                                && cancel.IsEnabled
                                && ConnectorMod.IsNodeVisible(cancel);
        var surface = new CardBundleSelectionSurface(
            SurfaceKind,
            stage,
            entities.GetId(screen, "screen"),
            prompt,
            selectedId,
            selectableBundleIds,
            canConfirm,
            canCancelPreview,
            bundles);
        bool hasCurrentCommand = selectableBundleIds.Length > 0
                                 || canConfirm
                                 || canCancelPreview;
        string readiness = hasCurrentCommand ? "ready" : "settling";
        var completeness = new StateCompleteness(
            "contract_complete_for_two_stage_visible_card_bundle_selection",
            hasCurrentCommand
                ? "derived_from_current_bundle_hitboxes_or_preview_controls"
                : "temporarily_empty_while_bundle_ui_settles",
            new[]
            {
                "NChooseABundleSelectionScreen visible overlay",
                "NCardBundle.Bundle+Hitbox",
                "NChooseABundleSelectionScreen.%BundlePreviewContainer+%Cards",
                "NChooseABundleSelectionScreen.%Confirm+%Cancel"
            },
            Array.Empty<string>());
        string signature = StableIdentityHash.Object(new
        {
            game.Version,
            surface
        });
        return new LiveObservation(
            signature,
            readiness,
            LiveContextReader.Build(entities),
            surface,
            completeness,
            game,
            Array.Empty<string>());
    }

    private static NativeInputResult StartPreview(
        NChooseABundleSelectionScreen expectedScreen,
        NCardBundle expectedBundle,
        Control expectedPreview,
        Control expectedPreviewCards)
    {
        if (!IsCurrent(expectedScreen)
            || expectedPreview.Visible
            || !ConnectorMod.FindAll<NCardBundle>(expectedScreen).Any(bundle => ReferenceEquals(bundle, expectedBundle))
            || !ConnectorMod.IsNodeVisible(expectedBundle)
            || expectedBundle.Hitbox is not { IsEnabled: true })
        {
            return NativeInputResult.Rejected("bundle_choice_changed", "The advertised bundle is no longer selectable.");
        }

        expectedBundle.Hitbox.ForceClick();
        return NativeInputResult.Delivered(PreviewDeliveryEvidence);
    }

    private static NativeInputResult StartConfirm(
        NChooseABundleSelectionScreen expectedScreen,
        NCardBundle expectedBundle,
        NConfirmButton expectedConfirm)
    {
        Control? previewCards = expectedScreen.GetNodeOrNull<Control>("%Cards");
        if (!IsCurrent(expectedScreen)
            || previewCards == null
            || !ReferenceEquals(ResolvePreviewedBundle(
                expectedScreen,
                ConnectorMod.FindAll<NCardBundle>(expectedScreen).ToArray()), expectedBundle)
            || !expectedConfirm.IsEnabled
            || !ConnectorMod.IsNodeVisible(expectedConfirm))
        {
            return NativeInputResult.Rejected("bundle_preview_changed", "The previewed bundle is no longer confirmable.");
        }

        expectedConfirm.ForceClick();
        return NativeInputResult.Delivered(ConfirmDeliveryEvidence);
    }

    private static NativeInputResult StartCancel(
        NChooseABundleSelectionScreen expectedScreen,
        NCardBundle expectedBundle,
        NBackButton expectedCancel,
        Control expectedPreview)
    {
        Control? previewCards = expectedScreen.GetNodeOrNull<Control>("%Cards");
        if (!IsCurrent(expectedScreen)
            || previewCards == null
            || !ReferenceEquals(ResolvePreviewedBundle(
                expectedScreen,
                ConnectorMod.FindAll<NCardBundle>(expectedScreen).ToArray()), expectedBundle)
            || !expectedCancel.IsEnabled
            || !ConnectorMod.IsNodeVisible(expectedCancel))
        {
            return NativeInputResult.Rejected("bundle_preview_changed", "The previewed bundle is no longer cancelable.");
        }

        expectedCancel.ForceClick();
        return NativeInputResult.Delivered(CancelPreviewDeliveryEvidence);
    }

    internal static NativeInputResult StartDirectPreview(
        NativeEntityRegistry entities,
        string screenId,
        string bundleId)
    {
        if (!TryResolveDirect(
                entities,
                screenId,
                bundleId,
                out NChooseABundleSelectionScreen? screen,
                out NCardBundle? bundle,
                out NativeInputResult? rejection)
            || screen == null
            || bundle == null)
        {
            return rejection!;
        }
        Control? preview = screen.GetNodeOrNull<Control>("%BundlePreviewContainer");
        Control? previewCards = screen.GetNodeOrNull<Control>("%Cards");
        return preview == null || previewCards == null
            ? NativeInputResult.Rejected(
                "bundle_controls_changed",
                "The exact bundle preview controls are no longer available.")
            : StartPreview(screen, bundle, preview, previewCards);
    }

    internal static NativeInputResult StartDirectConfirm(
        NativeEntityRegistry entities,
        string screenId,
        string bundleId)
    {
        if (!TryResolveDirect(
                entities,
                screenId,
                bundleId,
                out NChooseABundleSelectionScreen? screen,
                out NCardBundle? bundle,
                out NativeInputResult? rejection)
            || screen == null
            || bundle == null)
        {
            return rejection!;
        }
        NConfirmButton? confirm = screen.GetNodeOrNull<NConfirmButton>("%Confirm");
        return confirm == null
            ? NativeInputResult.Rejected(
                "bundle_controls_changed",
                "The exact bundle confirmation control is no longer available.")
            : StartConfirm(screen, bundle, confirm);
    }

    internal static NativeInputResult StartDirectCancel(
        NativeEntityRegistry entities,
        string screenId,
        string bundleId)
    {
        if (!TryResolveDirect(
                entities,
                screenId,
                bundleId,
                out NChooseABundleSelectionScreen? screen,
                out NCardBundle? bundle,
                out NativeInputResult? rejection)
            || screen == null
            || bundle == null)
        {
            return rejection!;
        }
        NBackButton? cancel = screen.GetNodeOrNull<NBackButton>("%Cancel");
        Control? preview = screen.GetNodeOrNull<Control>("%BundlePreviewContainer");
        return cancel == null || preview == null
            ? NativeInputResult.Rejected(
                "bundle_controls_changed",
                "The exact bundle cancel controls are no longer available.")
            : StartCancel(screen, bundle, cancel, preview);
    }

    private static bool TryResolveDirect(
        NativeEntityRegistry entities,
        string screenId,
        string bundleId,
        out NChooseABundleSelectionScreen? screen,
        out NCardBundle? bundle,
        out NativeInputResult? rejection)
    {
        bundle = null;
        rejection = null;
        if (!entities.TryResolve(screenId, out screen)
            || screen == null
            || !IsCurrent(screen))
        {
            rejection = NativeInputResult.Rejected(
                "card_bundle_owner_changed",
                "The exact card-bundle owner is no longer current.");
            return false;
        }
        NCardBundle[] bundles = ConnectorMod.FindAll<NCardBundle>(screen)
            .Where(ConnectorMod.IsLiveNode)
            .ToArray();
        if (!entities.TryResolve(bundleId, out NCardBundle? resolvedBundle)
            || resolvedBundle == null
            || !bundles.Any(candidate => ReferenceEquals(candidate, resolvedBundle)))
        {
            rejection = NativeInputResult.Rejected(
                "card_bundle_changed",
                "The exact visible bundle changed.");
            return false;
        }
        bundle = resolvedBundle;
        return true;
    }

    private static NCardBundle? ResolvePreviewedBundle(
        NChooseABundleSelectionScreen screen,
        IReadOnlyList<NCardBundle> bundles)
    {
        NCardBundle? selected;
        try
        {
            selected = screen.Get(
                NChooseABundleSelectionScreen.PropertyName._selectedBundle)
                .As<NCardBundle>();
        }
        catch
        {
            return null;
        }
        return ResolveUniqueReference(bundles, selected);
    }

    internal static T? ResolveUniqueReference<T>(
        IReadOnlyList<T> candidates,
        T? selected)
        where T : class
    {
        if (selected == null)
            return null;
        T[] matches = candidates
            .Where(candidate => ReferenceEquals(candidate, selected))
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static bool IsCurrent(NChooseABundleSelectionScreen screen) =>
        ActiveInputResolver.IsVisibleActiveOverlay(screen)
        && ReferenceEquals(NOverlayStack.Instance?.Peek(), screen);

    private static string? ReadText(MegaCrit.Sts2.addons.mega_text.MegaLabel label)
    {
        try
        {
            string value = label.Text?.ToString() ?? string.Empty;
            value = ConnectorMod.StripRichTextTags(value).Trim();
            return value.Length == 0 ? null : value;
        }
        catch
        {
            return null;
        }
    }

    private static LiveObservation BindingUnavailable(
        GameBuildIdentity game,
        ILiveContext context,
        string reason)
    {
        var surface = new UnsupportedSurface("unsupported", SurfaceKind, reason);
        var completeness = new StateCompleteness(
            "partial",
            "empty_fail_closed",
            new[] { "NChooseABundleSelectionScreen exact-version binding" },
            new[] { "bundle_membership", "stage", "legal_actions" });
        string signature = StableIdentityHash.Object(new { game.Version, reason });
        return new LiveObservation(
            signature,
            "degraded",
            context,
            surface,
            completeness,
            game,
            new[] { "card_bundle_selection_binding_unavailable" })
        {
            Diagnostics = new[]
            {
                HostDiagnostics.Create(
                    "host.surface.card_bundle_selection.binding_unavailable",
                    "error",
                    "surface",
                    "actions_suppressed",
                    "update_host_adapter",
                    reason)
            }
        };
    }
}
