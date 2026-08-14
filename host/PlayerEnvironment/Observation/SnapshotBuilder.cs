using STS2Connector.Authority;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Connector.LiveHost;
using STS2Connector.LiveHost.Contracts;
using STS2Connector.PlayerEnvironment.Protocol;
using STS2Connector.NativeUi;

namespace STS2Connector.PlayerEnvironment;

internal static partial class PlayerEnvironmentService
{
    internal static SnapshotBuildResult BuildSnapshot(
        bool suppressNativePageEvidence = true)
    {
        GameBuildIdentity game = EnvironmentIdentityRuntime.ReadGame();
        LiveObservation? sourceFreeSurface =
            NativeGeneratedCardChoice.TryBuild(Entities, game)
            ?? NativeSimpleCardSelection.TryBuild(Entities, game)
            ?? NativeDeckTransformSelection.TryBuild(Entities, game)
            ?? NativeCombatPileSelection.TryBuild(Entities, game)
            ?? NativeDeckCardSelection.TryBuild(Entities, game)
            ?? NativeRestSite.TryBuild(Entities, game);
        LiveObservation draft = sourceFreeSurface
            ?? LiveObservationReader.Build(Entities, game);
        if (suppressNativePageEvidence)
            draft = NativePageEvidence.SuppressMutation(draft);
        draft = draft with
        {
            InputOwnership = draft.Surface is UnsupportedSurface
                ? new InputOwnership(
                    "none_fail_closed",
                    null,
                    "An unsupported Surface cannot own Player Environment input.")
                : new InputOwnership(
                    "current_ui_owned",
                    draft.Surface.Kind,
                    "The exact current native UI owns input.")
        };

        PersistentVisibleStateBuildResult shared = game.Compatibility.StateObservationAllowed
            ? PersistentVisibleStateReader.Build(Entities)
            : new PersistentVisibleStateBuildResult(false, null, null);
        draft = LiveObservationReader.ApplyMissingPersistentStatePolicy(draft, shared);
        bool shopCatalogAvailable = ShopSurfaceFacts.TryGetCurrent(out _, out _, out _);
        PlayerVisibilityProjection information = PlayerVisibilityCatalog.Build(
            draft,
            shared.State != null,
            shopCatalogAvailable);
        IReadOnlyList<PlayerReadCatalogEntry> readCatalog = information.ReadCatalog;
        IReadOnlyList<PlayerEnvironmentLinkedDetailCatalogEntry> linkedDetails =
            BuildLinkedDetailCatalog(draft.Surface);
        PlayerVisibilityState visibility = information.Visibility with
        {
            AvailableReads = readCatalog.Select(entry => entry.Kind).ToArray(),
            LinkedDetailKinds = linkedDetails.Select(entry => entry.Kind)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(kind => kind, StringComparer.Ordinal)
                .ToArray(),
            Missing = information.Visibility.Missing
                .Where(value => value != "linked_entity_detail_catalog_not_implemented")
                .ToArray()
        };
        JsonNode rawSurface = JsonSerializer.SerializeToNode(
            draft.Surface,
            draft.Surface.GetType(),
            ConnectorMod._jsonOptions) ?? new JsonObject();
        PlayerEnvironmentInteractionContent surfaceContent =
            ProjectVisibleFacts(draft.Surface, draft.Context);
        IReadOnlyList<NativeUiBoundAction> nativeBindings =
            BuildPlayerEnvironmentBindings(draft);
        string interactionId = ReadFirstString(
            rawSurface,
            "screen_entity_id",
            "room_entity_id",
            "hand_entity_id",
            "map_screen_entity_id")
            ?? nativeBindings.SelectMany(item => item.Candidate.EntityBindings)
                .FirstOrDefault(entity => IsOwnerRole(entity.Role))?.EntityId
            ?? "interaction_" + StableIdentityHash.Object(new { draft.Surface.Kind, draft.Signature })[..20];
        Dictionary<string, PlayerEnvironmentReferent> referents =
            BuildFactReferents(surfaceContent);
        BoundActionProjectionResult projected =
            ProjectBoundActions(nativeBindings, interactionId, referents);
        IReadOnlyList<PlayerEnvironmentInteractionCapability> capabilities =
            ProjectInteractionCapabilities(projected.Projection, referents);
        string stage = ReadFirstString(rawSurface, "stage") ?? draft.Readiness;
        string? prompt = ReadFirstString(rawSurface, "prompt", "body", "message");
        IReadOnlyList<PlayerEnvironmentReadOpportunity> reads = readCatalog
            .Select(entry => new PlayerEnvironmentReadOpportunity(
                $"read:{entry.Kind}",
                entry.Kind,
                null,
                PlayerEnvironmentContract.ReadContentSchema(entry.Kind),
                entry.VisibilityBasis,
                SnapshotBound: true,
                entry.OrderingSemantics,
                entry.HiddenByPolicy))
            .Concat(linkedDetails.Select(entry => new PlayerEnvironmentReadOpportunity(
                $"read:{entry.Kind}:{entry.EntityId}",
                entry.Kind,
                entry.EntityId,
                PlayerEnvironmentContract.ReadContentSchema(entry.Kind),
                entry.VisibilityBasis,
                SnapshotBound: true,
                "single_entity",
                Array.Empty<string>())))
            .OrderBy(read => read.ReadId, StringComparer.Ordinal)
            .ToArray();
        string signature = StableIdentityHash.Object(new
        {
            game.Version,
            game.Commit,
            shared.State,
            draft.Readiness,
            surface = surfaceContent,
            interactionId,
            referents = referents.Values.OrderBy(item => item.ReferentId, StringComparer.Ordinal).ToArray(),
            authority = CanonicalAuthoritySignature(nativeBindings),
            reads,
            visibility.HiddenByPolicy
        });
        (string snapshotId, long sequence) = SnapshotIdentity.Observe(signature);
        bool visibleUnsupported = draft.Surface is UnsupportedSurface;
        bool actionsPublished = projected.Projection.Status == "complete"
            && projected.Projection.MaterializedCount > 0;
        string status = actionsPublished
            ? "interactive"
            : visibleUnsupported ? "visible_unsupported" : draft.Readiness == "settling" ? "settling" : "observed";
        PlayerEnvironmentCompleteness completeness = ToCompleteness(
            draft.Completeness,
            visibility.HiddenByPolicy,
            visibleUnsupported ? "visible_unmapped" : null);
        if (projected.Projection.Status != "complete")
        {
            completeness = completeness with
            {
                Status = "partial",
                Missing = completeness.Missing
                    .Append("finite_bound_action_projection_incomplete")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            };
        }
        var snapshot = new PlayerEnvironmentSnapshot(
            PlayerEnvironmentContract.ProtocolVersion,
            PlayerEnvironmentContract.SnapshotSchema,
            snapshotId,
            sequence,
            DateTimeOffset.UtcNow,
            status,
            shared.State == null
                ? null
                : new PlayerEnvironmentContent(
                    "sts2.player-environment/persistent/run-player-1",
                    JsonSerializer.SerializeToNode(shared.State, ConnectorMod._jsonOptions) ?? new JsonObject()),
            new PlayerEnvironmentInteraction(
                interactionId,
                draft.Surface.Kind,
                stage,
                prompt,
                SurfaceContentSchema(draft.Surface.Kind),
                surfaceContent,
                capabilities),
            referents.Values.OrderBy(item => item.ReferentId, StringComparer.Ordinal).ToArray(),
            projected.Projection,
            reads,
            completeness,
            ToSessionReference(EnvironmentIdentityRuntime.HostIdentity(), game),
            ToInformationPolicy(EnvironmentIdentityRuntime.InformationPolicy()));
        return new SnapshotBuildResult(
            snapshot,
            draft,
            projected.Bindings);
    }

    internal static PlayerEnvironmentHostIdentity ToHostIdentity(LiveHostIdentity identity) => new(
        PlayerEnvironmentContract.EnvironmentId,
        PlayerEnvironmentContract.EnvironmentName,
        identity.Version,
        identity.RuntimeInstanceId,
        "live_ui",
        new PlayerEnvironmentImplementationIdentity(
            identity.SourceRevision,
            identity.ModuleVersionId,
            identity.ArtifactSha256));

    internal static PlayerEnvironmentGameIdentity ToGameIdentity(GameBuildIdentity game)
    {
        ModsetIdentity? modset = game.Modset;
        return new PlayerEnvironmentGameIdentity(
            game.Version,
            game.Commit,
            game.Branch,
            game.MainAssemblyHash,
            new PlayerEnvironmentCompatibility(
                game.Compatibility.Status,
                game.Compatibility.StateObservationAllowed,
                game.Compatibility.Detail),
            new PlayerEnvironmentModset(
                modset?.Status ?? "unavailable",
                modset?.Fingerprint ?? "unavailable",
                modset?.FingerprintScope ?? "unavailable",
                modset?.Mods
                    .Where(item => string.Equals(
                        item.LoadState,
                        "Loaded",
                        StringComparison.Ordinal))
                    .Select(item => item.Id)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray() ?? Array.Empty<string>(),
                modset?.Detail ?? "No loaded Modset identity was available."));
    }

    private static PlayerEnvironmentSessionReference ToSessionReference(
        LiveHostIdentity identity,
        GameBuildIdentity game) => new(
            identity.RuntimeInstanceId,
            StableIdentityHash.Object(new
            {
                identity.ArtifactSha256,
                identity.ModuleVersionId,
                game.Version,
                game.Commit,
                game.MainAssemblyHash,
                Modset = game.Modset?.Fingerprint
            }));

    private static PlayerEnvironmentInformationPolicy ToInformationPolicy(
        InformationPolicyInfo policy) => new(
            policy.Id,
            policy.Scope,
            policy.IncludesHiddenInformation,
            policy.UnknownFieldBehavior);

    private static PlayerEnvironmentAttribution ToAttribution(
        MutationAttribution value) => new(
            value.RuntimeInstanceId,
            value.ClientSessionId,
            value.ClientInstanceId,
            value.ProductId,
            value.ProductName,
            value.ProductVersion,
            value.ControllerLeaseId,
            value.ControllerGeneration);

}
