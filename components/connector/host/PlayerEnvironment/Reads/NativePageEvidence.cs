using STS2Connector.NativeUi;
using STS2Connector.Authority;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Connector.LiveHost;
using STS2Connector.LiveHost.Contracts;
using STS2Connector.PlayerEnvironment.Protocol;

namespace STS2Connector.PlayerEnvironment;

internal sealed record PlayerEnvironmentNativePageOperationResult(
    PlayerEnvironmentNativePageResponse? Response,
    string? ErrorCode,
    string? Detail);

internal sealed record PlayerEnvironmentNativePageRuntimeSnapshot(
    string SnapshotId,
    string RuntimeInstanceId,
    PlayerEnvironmentNativePageOwner Owner,
    PlayerEnvironmentHostIdentity Host,
    PlayerEnvironmentGameIdentity Game,
    IReadOnlyList<string> ReadKinds)
{
    internal SnapshotBuildResult? AuthoritativeBuild { get; init; }
}

internal sealed record PlayerEnvironmentNativePageResult(
    PlayerEnvironmentNativePageRead? Page,
    string? ErrorCode,
    string? Detail)
{
    public static PlayerEnvironmentNativePageResult Success(
        PlayerEnvironmentNativePageRead page) => new(page, null, null);

    public static PlayerEnvironmentNativePageResult Failure(
        string code,
        string detail) => new(null, code, detail);
}

internal interface IPlayerEnvironmentNativePageHost
{
    bool HasOwnedPage { get; }

    PlayerEnvironmentNativePageRuntimeSnapshot Capture(
        IReadOnlyCollection<string>? requiredReadKinds = null);

    PlayerEnvironmentNativePageResult Open(
        string kind,
        PlayerEnvironmentNativePageRuntimeSnapshot pre);

    PlayerEnvironmentNativePageResult Read(string kind);

    PlayerEnvironmentNativePageResult Return(string kind);

    void Reset();
}

internal sealed class PlayerEnvironmentNativePageSession
{
    internal const string RunDeckKind = "run_deck";
    internal const string CombatDrawPileKind = "combat_draw_pile";
    internal const string CombatDiscardPileKind = "combat_discard_pile";
    internal const string CombatExhaustPileKind = "combat_exhaust_pile";
    internal const string ShopCatalogKind = "shop_catalog";

    private static readonly string[] Kinds =
    {
        RunDeckKind,
        CombatDrawPileKind,
        CombatDiscardPileKind,
        CombatExhaustPileKind,
        ShopCatalogKind
    };

    private readonly IPlayerEnvironmentNativePageHost _environment;
    private bool _enabled;
    private Session? _session;

    private sealed record Session(
        string Id,
        string Kind,
        string ExpectedSnapshotId,
        string ExpectedRuntimeInstanceId,
        PlayerEnvironmentNativePageRuntimeSnapshot Pre,
        PlayerEnvironmentNativePageRuntimeSnapshot Current,
        PlayerEnvironmentNativePageRuntimeSnapshot? Post,
        PlayerEnvironmentNativePageRead? Page,
        string Phase,
        string? ErrorCode,
        string? Detail);

    public PlayerEnvironmentNativePageSession(
        IPlayerEnvironmentNativePageHost environment)
    {
        _environment = environment;
    }

    public bool ReservesInputOwner =>
        _session != null && _session.Phase != "returned";

    public string? ActiveSessionId => ReservesInputOwner ? _session?.Id : null;

    public void Configure(bool enabled) => _enabled = enabled;

    public PlayerEnvironmentEvidenceProfile Capability() => new(
        PlayerEnvironmentContract.NativePageEvidenceProfile,
        _enabled,
        Kinds,
        SnapshotBound: true,
        RuntimeBound: true,
        DefaultInConsumerFlow: false,
        CreatesMutationAuthority: false,
        EntersActionLedger: false);

    public PlayerEnvironmentNativePageOperationResult Open(
        PlayerEnvironmentNativePageOpenRequest request)
    {
        string profile = request.Profile ?? string.Empty;
        string kind = request.Kind ?? string.Empty;
        string expectedSnapshotId = request.ExpectedSnapshotId ?? string.Empty;
        string expectedRuntime = request.ExpectedRuntimeInstanceId ?? string.Empty;

        if (!_enabled)
            return Failure("native_page_evidence_disabled", "The optional native-page evidence profile is disabled in STS2_MCP.conf.");
        if (!string.Equals(profile, PlayerEnvironmentContract.NativePageEvidenceProfile, StringComparison.Ordinal))
            return Failure("native_page_evidence_profile_mismatch", "The request does not name the configured native-page evidence profile.");
        if (!Kinds.Contains(kind, StringComparer.Ordinal))
            return Failure("native_page_evidence_kind_not_supported", "The requested native page is outside the fixed evidence profile.");
        if (ReservesInputOwner)
            return Failure("native_page_evidence_session_active", "Return or recover the active native-page evidence session first.");

        string readKind = ReadKind(kind);
        PlayerEnvironmentNativePageRuntimeSnapshot current = _environment.Capture(
            new[] { readKind });
        if (!string.Equals(current.RuntimeInstanceId, expectedRuntime, StringComparison.Ordinal))
            return Failure("runtime_instance_changed", "The expected Host runtime instance is no longer loaded.");
        if (!string.Equals(current.SnapshotId, expectedSnapshotId, StringComparison.Ordinal))
            return Failure("stale_state", "The expected state token is no longer current; obtain a fresh observation before opening a native page.");

        if (!current.ReadKinds.Contains(readKind, StringComparer.Ordinal))
            return Failure("native_page_evidence_not_available", "The matching Player Environment Read is not advertised for the exact current state.");

        string sessionId = CreateSessionId(
            expectedSnapshotId,
            expectedRuntime,
            kind);
        PlayerEnvironmentNativePageResult opened = _environment.Open(kind, current);
        if (opened.Page == null)
        {
            if (!_environment.HasOwnedPage)
            {
                return Failure(
                    opened.ErrorCode ?? "native_page_evidence_open_failed",
                    opened.Detail ?? "The native page failed closed before an evidence session was created.");
            }

            _session = new Session(
                sessionId,
                kind,
                expectedSnapshotId,
                expectedRuntime,
                current,
                current,
                null,
                null,
                "recovery_required",
                opened.ErrorCode ?? "native_page_recovery_required",
                opened.Detail ?? "The exact native page may still own input and must be returned before commands resume.");
            return Success(_session);
        }

        PlayerEnvironmentNativePageRuntimeSnapshot afterOpen;
        try
        {
            afterOpen = _environment.Capture();
        }
        catch (Exception exception)
        {
            PlayerEnvironmentNativePageResult recovery =
                _environment.Return(kind);
            _session = new Session(
                sessionId,
                kind,
                expectedSnapshotId,
                expectedRuntime,
                current,
                current,
                null,
                opened.Page,
                "recovery_required",
                recovery.ErrorCode ?? "native_page_evidence_open_verification_failed",
                $"Post-open runtime verification failed closed with {exception.GetType().Name}; the exact page return was requested and restoration still requires verification.");
            return Success(_session);
        }
        _session = new Session(
            sessionId,
            kind,
            expectedSnapshotId,
            expectedRuntime,
            current,
            afterOpen,
            null,
            opened.Page,
            "open",
            null,
            null);
        return Success(_session);
    }

    public PlayerEnvironmentNativePageOperationResult Read(
        string sessionId,
        string expectedRuntimeInstanceId)
    {
        PlayerEnvironmentNativePageOperationResult? validation =
            ValidateSession(sessionId, expectedRuntimeInstanceId);
        if (validation != null)
            return validation;

        Session session = _session!;
        PlayerEnvironmentNativePageRuntimeSnapshot current = _environment.Capture();
        if (IsOwnerRestored(session.Pre, current))
        {
            _environment.Reset();
            _session = session with
            {
                Current = current,
                Post = current,
                Page = null,
                Phase = "returned",
                ErrorCode = null,
                Detail = null
            };
            return Success(_session);
        }
        if (session.Phase == "returned")
            return Success(session);

        PlayerEnvironmentNativePageResult read = _environment.Read(session.Kind);
        if (read.Page == null)
        {
            _session = session with
            {
                Current = current,
                Phase = "recovery_required",
                ErrorCode = read.ErrorCode ?? "native_page_evidence_read_failed",
                Detail = read.Detail ?? "The exact native page is no longer readable."
            };
            return Success(_session);
        }

        _session = session with
        {
            Current = current,
            Page = read.Page,
            Phase = "open",
            ErrorCode = null,
            Detail = null
        };
        return Success(_session);
    }

    public PlayerEnvironmentNativePageOperationResult Return(
        string sessionId,
        PlayerEnvironmentNativePageReturnRequest request)
    {
        if (!string.Equals(
                request.Profile,
                PlayerEnvironmentContract.NativePageEvidenceProfile,
                StringComparison.Ordinal))
        {
            return Failure("native_page_evidence_profile_mismatch", "The return request does not name the active evidence profile.");
        }

        PlayerEnvironmentNativePageOperationResult? validation =
            ValidateSession(sessionId, request.ExpectedRuntimeInstanceId ?? string.Empty);
        if (validation != null)
            return validation;

        Session session = _session!;
        PlayerEnvironmentNativePageRuntimeSnapshot beforeReturn = _environment.Capture();
        if (IsOwnerRestored(session.Pre, beforeReturn))
        {
            _environment.Reset();
            _session = session with
            {
                Current = beforeReturn,
                Post = beforeReturn,
                Page = null,
                Phase = "returned",
                ErrorCode = null,
                Detail = null
            };
            return Success(_session);
        }

        PlayerEnvironmentNativePageResult returned = _environment.Return(session.Kind);
        if (returned.ErrorCode != null)
        {
            _session = session with
            {
                Current = beforeReturn,
                Phase = "recovery_required",
                ErrorCode = returned.ErrorCode,
                Detail = returned.Detail
            };
            return Success(_session);
        }

        PlayerEnvironmentNativePageRuntimeSnapshot post = _environment.Capture();
        bool restored = IsOwnerRestored(session.Pre, post);
        if (restored)
            _environment.Reset();
        _session = session with
        {
            Current = post,
            Post = restored ? post : null,
            Page = restored ? null : session.Page,
            Phase = restored ? "returned" : "recovery_required",
            ErrorCode = restored ? null : "pre_owner_not_restored",
            Detail = restored
                ? null
                : "The native return was requested, but the exact pre-page owner and state have not yet been restored. Poll or retry return; never submit a game command from this session."
        };
        return Success(_session);
    }

    public LiveObservation SuppressMutation(LiveObservation draft)
    {
        if (!ReservesInputOwner)
            return draft;
        string sessionId = _session!.Id;
        return draft with
        {
            Signature = StableIdentityHash.Object(new
            {
                draft.Signature,
                nativePageEvidenceSession = sessionId
            }),
            Readiness = "settling",
            Surface = new NoActionSurface(
                "no_action",
                "native_page_evidence_active",
                "An operator-owned native-page evidence session temporarily owns input; no game command is authorized."),
            Completeness = new StateCompleteness(
                draft.Completeness.PlayerVisibleSemantics,
                "empty_fail_closed",
                draft.Completeness.Sources
                    .Concat(new[] { "player_environment_native_page_input_reservation" })
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                draft.Completeness.Missing
                    .Concat(new[] { "mutation_suppressed_during_native_page_evidence" })
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()),
            Warnings = draft.Warnings
                .Concat(new[] { $"native_page_evidence_session_active:{sessionId}" })
                .ToArray()
        };
    }

    internal static string ReadKind(string kind) => kind switch
    {
        RunDeckKind => PlayerVisibleReadBuilder.RunDeckKind,
        CombatDrawPileKind or CombatDiscardPileKind or CombatExhaustPileKind =>
            PlayerVisibleReadBuilder.CombatPilesKind,
        ShopCatalogKind => PlayerVisibleReadBuilder.ShopCatalogKind,
        _ => string.Empty
    };

    private PlayerEnvironmentNativePageOperationResult? ValidateSession(
        string sessionId,
        string expectedRuntimeInstanceId)
    {
        if (_session == null
            || !string.Equals(_session.Id, sessionId, StringComparison.Ordinal))
        {
            return Failure("native_page_evidence_session_not_found", "No matching native-page evidence session exists in this runtime.");
        }
        PlayerEnvironmentNativePageRuntimeSnapshot current = _environment.Capture();
        if (!string.Equals(
                _session.ExpectedRuntimeInstanceId,
                expectedRuntimeInstanceId,
                StringComparison.Ordinal)
            || !string.Equals(
                current.RuntimeInstanceId,
                expectedRuntimeInstanceId,
                StringComparison.Ordinal))
        {
            return Failure("runtime_instance_changed", "The evidence session is not bound to the currently loaded Host runtime.");
        }
        return null;
    }

    private static bool IsOwnerRestored(
        PlayerEnvironmentNativePageRuntimeSnapshot pre,
        PlayerEnvironmentNativePageRuntimeSnapshot current) =>
        string.Equals(pre.RuntimeInstanceId, current.RuntimeInstanceId, StringComparison.Ordinal)
        && pre.Owner == current.Owner;

    private static string CreateSessionId(
        string expectedSnapshotId,
        string expectedRuntime,
        string kind) =>
        "player_environment_" + StableIdentityHash.Object(new
        {
            expectedSnapshotId,
            expectedRuntime,
            kind,
            nonce = Guid.NewGuid().ToString("N")
        })[..20];

    private static PlayerEnvironmentNativePageOperationResult Failure(
        string code,
        string detail) => new(null, code, detail);

    private static PlayerEnvironmentNativePageOperationResult Success(Session session) =>
        new(new PlayerEnvironmentNativePageResponse(
            PlayerEnvironmentContract.ProtocolVersion,
            PlayerEnvironmentContract.NativePageEvidenceSchema,
            session.Id,
            PlayerEnvironmentContract.NativePageEvidenceProfile,
            session.Kind,
            session.Phase,
            session.ExpectedSnapshotId,
            session.Pre.SnapshotId,
            session.Current.SnapshotId,
            session.Post?.SnapshotId,
            session.ExpectedRuntimeInstanceId,
            session.Current.RuntimeInstanceId,
            session.Pre.Owner,
            session.Current.Owner,
            session.Post?.Owner,
            session.Page,
            session.Current.Host,
            session.Current.Game,
            CreatesMutationAuthority: false,
            EntersActionLedger: false,
            session.ErrorCode,
            session.Detail), null, null);
}

internal sealed class LiveNativePageEvidenceHost :
    IPlayerEnvironmentNativePageHost
{
    private readonly Func<IReadOnlyCollection<string>?, SnapshotBuildResult> _capture;
    private readonly NativeEntityRegistry _entities;
    private object? _activePage;
    private SnapshotBuildResult? _preBuild;

    public LiveNativePageEvidenceHost(
        Func<IReadOnlyCollection<string>?, SnapshotBuildResult> capture,
        NativeEntityRegistry entities)
    {
        _capture = capture;
        _entities = entities;
    }

    public bool HasOwnedPage => _activePage != null;

    public PlayerEnvironmentNativePageRuntimeSnapshot Capture(
        IReadOnlyCollection<string>? requiredReadKinds = null)
    {
        SnapshotBuildResult snapshot = _capture(requiredReadKinds);
        return new PlayerEnvironmentNativePageRuntimeSnapshot(
            snapshot.Snapshot.SnapshotId,
            snapshot.Snapshot.Session.RuntimeInstanceId,
            new PlayerEnvironmentNativePageOwner(
                snapshot.HostObservation.Context.Kind,
                snapshot.HostObservation.Surface.Kind,
                NativeOwner(snapshot.HostObservation)),
            PlayerEnvironmentService.ToHostIdentity(EnvironmentIdentityRuntime.HostIdentity()),
            PlayerEnvironmentService.ToGameIdentity(snapshot.HostObservation.Game),
            snapshot.Snapshot.Reads
                .Where(entry => entry.TargetReferentId == null)
                .Select(entry => entry.Kind)
                .ToArray())
        {
            AuthoritativeBuild = snapshot
        };
    }

    public PlayerEnvironmentNativePageResult Open(
        string kind,
        PlayerEnvironmentNativePageRuntimeSnapshot pre)
    {
        if (_activePage != null)
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_already_owned",
                "The evidence environment already owns a native page.");

        _preBuild = pre.AuthoritativeBuild;
        if (_preBuild == null)
        {
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_prebuild_missing",
                "The native page did not receive the authoritative pre-page build.");
        }
        try
        {
            PlayerEnvironmentNativePageResult result = kind switch
            {
                PlayerEnvironmentNativePageSession.RunDeckKind =>
                    OpenRunDeck(),
                PlayerEnvironmentNativePageSession.CombatDrawPileKind =>
                    OpenCombatPile(PileType.Draw),
                PlayerEnvironmentNativePageSession.CombatDiscardPileKind =>
                    OpenCombatPile(PileType.Discard),
                PlayerEnvironmentNativePageSession.CombatExhaustPileKind =>
                    OpenCombatPile(PileType.Exhaust),
                PlayerEnvironmentNativePageSession.ShopCatalogKind =>
                    OpenShopCatalog(),
                _ => PlayerEnvironmentNativePageResult.Failure(
                    "native_page_evidence_kind_not_supported",
                    "The requested native page kind is not implemented.")
            };
            if (result.Page == null)
            {
                if (_activePage == null)
                {
                    Reset();
                }
                else if (!TryRecoverOwnedPage())
                {
                    return PlayerEnvironmentNativePageResult.Failure(
                        "native_page_recovery_required",
                        $"{result.Detail ?? "Native page opening failed."} The exact page still owns input and must be returned before commands resume.");
                }
            }
            return result;
        }
        catch (Exception exception)
        {
            bool recovered = TryRecoverOwnedPage();
            return PlayerEnvironmentNativePageResult.Failure(
                recovered || _activePage == null
                    ? "native_page_open_failed"
                    : "native_page_recovery_required",
                recovered || _activePage == null
                    ? $"Native page opening failed closed with {exception.GetType().Name}."
                    : $"Native page opening failed with {exception.GetType().Name}; the exact page still owns input and must be returned before commands resume.");
        }
    }

    public PlayerEnvironmentNativePageResult Read(string kind)
    {
        if (_activePage == null || _preBuild == null)
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_not_owned",
                "This runtime no longer owns the exact native page.");
        try
        {
            if (!IsExactCurrentPage(kind))
                return PlayerEnvironmentNativePageResult.Failure(
                    "native_page_owner_changed",
                    "The exact native page is no longer the current input owner.");
            return BuildPageRead(kind);
        }
        catch (Exception exception)
        {
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_read_failed",
                $"Native page reading failed closed with {exception.GetType().Name}.");
        }
    }

    public PlayerEnvironmentNativePageResult Return(string kind)
    {
        if (_activePage == null)
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_not_owned",
                "This runtime does not own a native page to return from.");
        try
        {
            if (!IsExactCurrentPage(kind))
                return PlayerEnvironmentNativePageResult.Failure(
                    "native_page_owner_changed",
                    "Return was refused because another native owner is current.");

            if (_activePage is NDeckViewScreen or NCardPileScreen)
            {
                NCapstoneContainer.Instance?.Close();
            }
            else if (_activePage is NMerchantInventory inventory)
            {
                NBackButton back = inventory.GetNode<NBackButton>("%BackButton");
                if (!back.IsEnabled || !ConnectorMod.IsNodeVisible(back))
                {
                    return PlayerEnvironmentNativePageResult.Failure(
                        "native_return_control_unavailable",
                        "The exact merchant Back control is no longer current and enabled.");
                }
                back.ForceClick();
            }
            else
            {
                return PlayerEnvironmentNativePageResult.Failure(
                    "native_page_type_changed",
                    "The owned native page type is no longer recognized.");
            }

            return new PlayerEnvironmentNativePageResult(null, null, null);
        }
        catch (Exception exception)
        {
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_return_failed",
                $"Native page return failed closed with {exception.GetType().Name}.");
        }
    }

    public void Reset()
    {
        _activePage = null;
        _preBuild = null;
    }

    private PlayerEnvironmentNativePageResult OpenRunDeck()
    {
        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        Player? player = runState == null ? null : LocalContext.GetMe(runState);
        NCapstoneContainer? capstone = NCapstoneContainer.Instance;
        NTopBarDeckButton? deckButton = NRun.Instance?.GlobalUi.TopBar.Deck;
        if (player == null
            || capstone == null
            || capstone.InUse
            || deckButton == null
            || !deckButton.IsEnabled
            || !ConnectorMod.IsNodeVisible(deckButton))
        {
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_not_openable",
                "The exact player Deck control is not currently openable.");
        }

        deckButton.ForceClick();
        if (capstone.CurrentCapstoneScreen is not NDeckViewScreen screen)
        {
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_owner_not_established",
                "The native Deck page did not become the exact current owner.");
        }
        _activePage = screen;
        if (!ActiveScreenContext.Instance.IsCurrent(screen))
        {
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_owner_not_established",
                "The native Deck page did not become the exact current owner.");
        }
        return BuildPageRead(PlayerEnvironmentNativePageSession.RunDeckKind);
    }

    private PlayerEnvironmentNativePageResult OpenCombatPile(PileType pileType)
    {
        NCombatRoom? room = NCombatRoom.Instance;
        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        Player? player = runState == null ? null : LocalContext.GetMe(runState);
        PlayerCombatState? combat = player?.PlayerCombatState;
        NCapstoneContainer? capstone = NCapstoneContainer.Instance;
        NCombatCardPile? button = pileType switch
        {
            PileType.Draw => room?.Ui.DrawPile,
            PileType.Discard => room?.Ui.DiscardPile,
            PileType.Exhaust => room?.Ui.ExhaustPile,
            _ => null
        };
        CardPile? pile = pileType switch
        {
            PileType.Draw => combat?.DrawPile,
            PileType.Discard => combat?.DiscardPile,
            PileType.Exhaust => combat?.ExhaustPile,
            _ => null
        };
        if (!CombatManager.Instance.IsInProgress
            || button == null
            || pile == null
            || pile.IsEmpty
            || capstone == null
            || capstone.InUse
            || !button.IsEnabled
            || !ConnectorMod.IsNodeVisible(button))
        {
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_not_openable",
                "The exact non-empty combat pile control is not currently openable.");
        }

        button.ForceClick();
        if (capstone.CurrentCapstoneScreen is not NCardPileScreen screen)
        {
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_owner_not_established",
                "The native combat-pile page did not become the exact current owner.");
        }
        _activePage = screen;
        if (!ReferenceEquals(screen.Pile, pile)
            || !ActiveScreenContext.Instance.IsCurrent(screen))
        {
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_owner_not_established",
                "The native combat-pile page did not become the exact current owner.");
        }
        return BuildPageRead(pileType switch
        {
            PileType.Draw => PlayerEnvironmentNativePageSession.CombatDrawPileKind,
            PileType.Discard => PlayerEnvironmentNativePageSession.CombatDiscardPileKind,
            _ => PlayerEnvironmentNativePageSession.CombatExhaustPileKind
        });
    }

    private PlayerEnvironmentNativePageResult OpenShopCatalog()
    {
        if (!ShopSurfaceFacts.TryGetCurrent(
                out MerchantRoom? merchantRoom,
                out NMerchantRoom? room,
                out MerchantInventory? inventory)
            || merchantRoom == null
            || room == null
            || inventory == null
            || !ShopSurfaceFacts.IsCurrentRoom(merchantRoom, room, inventory)
            || room.Inventory.IsOpen
            || !room.MerchantButton.IsEnabled
            || !ConnectorMod.IsNodeVisible(room.MerchantButton))
        {
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_not_openable",
                "The exact merchant inventory control is not currently openable.");
        }

        room.MerchantButton.ForceClick();
        if (room.Inventory.IsOpen)
            _activePage = room.Inventory;
        if (!ShopSurfaceFacts.IsCurrentInventory(merchantRoom, room, inventory))
        {
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_owner_not_established",
                "The merchant inventory did not become the exact current owner.");
        }
        _activePage = room.Inventory;
        return BuildPageRead(PlayerEnvironmentNativePageSession.ShopCatalogKind);
    }

    private PlayerEnvironmentNativePageResult BuildPageRead(string kind)
    {
        string readKind =
            PlayerEnvironmentNativePageSession.ReadKind(kind);
        SnapshotBuildResult? preBuild = _preBuild;
        if (preBuild == null
            || !preBuild.ReadBuilds.TryGetValue(
                readKind,
                out PlayerReadBuildResult? built)
            || built == null)
        {
            return PlayerEnvironmentNativePageResult.Failure(
                "native_page_read_materialization_missing",
                "The authoritative pre-page build did not materialize the requested read.");
        }
        if (built.Draft == null)
        {
            return PlayerEnvironmentNativePageResult.Failure(
                built.ErrorCode ?? "native_page_semantic_read_failed",
                built.Detail ?? "The matching Player Environment Read could not be rebuilt while the native page was open.");
        }

        string[] visibleEntities = _activePage switch
        {
            NDeckViewScreen deck => VisibleGridCards(deck),
            NCardPileScreen pile => VisibleGridCards(pile),
            NMerchantInventory inventory => inventory.GetAllSlots()
                .Where(ConnectorMod.IsNodeVisible)
                .Select(slot => _entities.GetId(slot, "shop_slot"))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            _ => Array.Empty<string>()
        };
        PlayerReadDraft draft = built.Draft;
        return PlayerEnvironmentNativePageResult.Success(
            new PlayerEnvironmentNativePageRead(
                _activePage!.GetType().FullName ?? _activePage.GetType().Name,
                readKind,
                visibleEntities.Length,
                visibleEntities,
                PlayerEnvironmentContract.ReadContentSchema(readKind),
                JsonSerializer.SerializeToNode(
                    draft.Content,
                    draft.Content.GetType(),
                    ConnectorMod._jsonOptions) ?? new JsonObject(),
                PlayerEnvironmentService.ToCompleteness(
                    draft.Completeness,
                    PlayerVisibleReadBuilder.HiddenByPolicyFor(readKind)),
                new[]
                {
                    "exact native player control ForceClick",
                    "ActiveScreenContext exact owner verification",
                    "current native page visible holder/slot projection",
                    "matching state-bound Player Environment Read rebuilt while page is open"
                }));
    }

    private string[] VisibleGridCards(NDeckViewScreen screen) =>
        screen.GetNode<NCardGrid>("CardGrid")
            .CurrentlyDisplayedCards
            .Select(card => _entities.GetId(card, "card"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private string[] VisibleGridCards(NCardPileScreen screen) =>
        screen.GetNode<NCardGrid>("CardGrid")
            .CurrentlyDisplayedCards
            .Select(card => _entities.GetId(card, "card"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private bool IsExactCurrentPage(string kind)
    {
        if (_activePage is NDeckViewScreen deck)
        {
            return kind == PlayerEnvironmentNativePageSession.RunDeckKind
                   && ReferenceEquals(NCapstoneContainer.Instance?.CurrentCapstoneScreen, deck)
                   && ActiveScreenContext.Instance.IsCurrent(deck);
        }
        if (_activePage is NCardPileScreen pile)
        {
            PileType expected = kind switch
            {
                PlayerEnvironmentNativePageSession.CombatDrawPileKind => PileType.Draw,
                PlayerEnvironmentNativePageSession.CombatDiscardPileKind => PileType.Discard,
                PlayerEnvironmentNativePageSession.CombatExhaustPileKind => PileType.Exhaust,
                _ => PileType.None
            };
            return pile.Pile.Type == expected
                   && ReferenceEquals(NCapstoneContainer.Instance?.CurrentCapstoneScreen, pile)
                   && ActiveScreenContext.Instance.IsCurrent(pile);
        }
        if (_activePage is NMerchantInventory inventory)
        {
            return kind == PlayerEnvironmentNativePageSession.ShopCatalogKind
                   && ShopSurfaceFacts.TryGetCurrent(
                       out MerchantRoom? merchantRoom,
                       out NMerchantRoom? room,
                       out MerchantInventory? model)
                   && merchantRoom != null
                   && room != null
                   && model != null
                   && ReferenceEquals(room.Inventory, inventory)
                   && ShopSurfaceFacts.IsCurrentInventory(merchantRoom, room, model);
        }
        return false;
    }

    private string NativeOwner(LiveObservation draft)
    {
        ICapstoneScreen? capstone = NCapstoneContainer.Instance?.CurrentCapstoneScreen;
        if (capstone != null)
            return $"capstone:{capstone.GetType().FullName}:{_entities.GetId(capstone, "native_page")}";
        if (ShopSurfaceFacts.TryGetCurrent(
                out MerchantRoom? merchantRoom,
                out NMerchantRoom? room,
                out MerchantInventory? inventory)
            && merchantRoom != null
            && room != null
            && inventory != null
            && ShopSurfaceFacts.IsCurrentInventory(merchantRoom, room, inventory))
        {
            return $"shop_inventory:{_entities.GetId(room.Inventory, "native_page")}";
        }
        return $"semantic:{draft.Context.Kind}:{draft.Surface.Kind}";
    }

    private bool TryRecoverOwnedPage()
    {
        if (_activePage == null)
        {
            Reset();
            return true;
        }

        try
        {
            if (_activePage is NDeckViewScreen or NCardPileScreen
                && ReferenceEquals(
                    NCapstoneContainer.Instance?.CurrentCapstoneScreen,
                    _activePage))
            {
                NCapstoneContainer.Instance?.Close();
            }
            else if (_activePage is NMerchantInventory inventory
                     && inventory.IsOpen)
            {
                NBackButton back = inventory.GetNode<NBackButton>("%BackButton");
                if (back.IsEnabled && ConnectorMod.IsNodeVisible(back))
                    back.ForceClick();
            }

            bool restored = _activePage switch
            {
                NDeckViewScreen or NCardPileScreen =>
                    !ReferenceEquals(
                        NCapstoneContainer.Instance?.CurrentCapstoneScreen,
                        _activePage),
                NMerchantInventory inventory => !inventory.IsOpen,
                _ => false
            };
            if (restored)
            {
                Reset();
                return true;
            }
        }
        catch
        {
            // Recovery never closes an owner that is not the exact page opened here.
        }
        return false;
    }
}

internal static partial class PlayerEnvironmentService
{
    public static void ConfigureNativePageEvidence(bool enabled) =>
        NativePageEvidence.Configure(enabled);

    public static PlayerEnvironmentNativePageOperationResult OpenNativePageEvidence(
        PlayerEnvironmentNativePageOpenRequest request) =>
        NativePageEvidence.Open(request);

    public static PlayerEnvironmentNativePageOperationResult ReadNativePageEvidence(
        string sessionId,
        string expectedRuntimeInstanceId) =>
        NativePageEvidence.Read(sessionId, expectedRuntimeInstanceId);

    public static PlayerEnvironmentNativePageOperationResult ReturnNativePageEvidence(
        string sessionId,
        PlayerEnvironmentNativePageReturnRequest request) =>
        NativePageEvidence.Return(sessionId, request);
}
