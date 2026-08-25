using System.Text.Json;
using System.Text.Json.Serialization;
using STS2Connector.PlayerEnvironment.Protocol;
using STS2HumanAnnotator.Core;

namespace STS2PlatformLiveUi;

public sealed record PolicyRuntimeHttpStatusResponse(
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("status")] PolicyRuntimeStatus Status);

public sealed record PolicyRuntimeTickResponse(
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("results")] IReadOnlyList<PolicyRuntimeTickResult> Results,
    [property: JsonPropertyName("status")] PolicyRuntimeStatus Status);

public sealed record PolicyRuntimeTickResult(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("status")] PolicyRuntimeStatus? Status);

public sealed record PolicyRuntimeStatus(
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("runtime")] PolicyRuntimeSoftware Runtime,
    [property: JsonPropertyName("policy")] PolicyRuntimePolicy Policy,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("lifecycle")] string Lifecycle,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("controller")] string Controller,
    [property: JsonPropertyName("tainted")] bool Tainted,
    [property: JsonPropertyName("taint_reason")] string? TaintReason,
    [property: JsonPropertyName("refreshing")] bool Refreshing,
    [property: JsonPropertyName("last_snapshot_id")] string? LastSnapshotId,
    [property: JsonPropertyName("last_snapshot")] PolicyRuntimeSnapshotStatus? LastSnapshot,
    [property: JsonPropertyName("last_decision")] PolicyRuntimeDecisionStatus? LastDecision,
    [property: JsonPropertyName("last_receipt")] PolicyRuntimeReceiptStatus? LastReceipt,
    [property: JsonPropertyName("reads")] IReadOnlyList<PolicyRuntimeReadStatus> Reads,
    [property: JsonPropertyName("invalidations")] IReadOnlyList<string> Invalidations,
    [property: JsonPropertyName("errors")] IReadOnlyList<string> Errors,
    [property: JsonPropertyName("environment")] PolicyRuntimeEnvironmentStatus? Environment)
{
    public const string CurrentSchema = "sts2.policy-runtime/status-1";
}

public sealed record PolicyRuntimeSoftware(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("code_sha256")] string? CodeSha256);

public sealed record PolicyRuntimePolicy(
    [property: JsonPropertyName("manifest_id")] string ManifestId,
    [property: JsonPropertyName("policy_id")] string PolicyId,
    [property: JsonPropertyName("policy_version")] string PolicyVersion,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("architecture")] string Architecture,
    [property: JsonPropertyName("artifact_sha256")] string ArtifactSha256);

public sealed record PolicyRuntimeSnapshotStatus(
    [property: JsonPropertyName("snapshot_id")] string SnapshotId,
    [property: JsonPropertyName("sequence")] int Sequence,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("runtime_instance_id")] string RuntimeInstanceId,
    [property: JsonPropertyName("environment_fingerprint")] string EnvironmentFingerprint);

public sealed record PolicyRuntimeDecisionStatus(
    [property: JsonPropertyName("decision_id")] string DecisionId,
    [property: JsonPropertyName("candidate_digest")] string CandidateDigest,
    [property: JsonPropertyName("candidate_count")] int CandidateCount,
    [property: JsonPropertyName("scores")] IReadOnlyList<double> Scores,
    [property: JsonPropertyName("selected_index")] int? SelectedIndex,
    [property: JsonPropertyName("bound_action_id")] string? BoundActionId,
    [property: JsonPropertyName("bound_action_label")] string? BoundActionLabel);

public sealed record PolicyRuntimeReceiptStatus(
    [property: JsonPropertyName("request_id")] string RequestId,
    [property: JsonPropertyName("delivery")] string Delivery,
    [property: JsonPropertyName("reason_code")] string? ReasonCode,
    [property: JsonPropertyName("successor_snapshot_id")] string? SuccessorSnapshotId);

public sealed record PolicyRuntimeReadStatus(
    [property: JsonPropertyName("read_id")] string ReadId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("content_schema")] string ContentSchema,
    [property: JsonPropertyName("target_referent_id")] string? TargetReferentId);

public sealed record PolicyRuntimeEnvironmentStatus(
    [property: JsonPropertyName("runtime_instance_id")] string RuntimeInstanceId,
    [property: JsonPropertyName("environment_fingerprint")] string EnvironmentFingerprint,
    [property: JsonPropertyName("host_kind")] string HostKind,
    [property: JsonPropertyName("connector_protocol_version")] string ConnectorProtocolVersion,
    [property: JsonPropertyName("connector_version")] string ConnectorVersion,
    [property: JsonPropertyName("connector_source_revision")] string? ConnectorSourceRevision,
    [property: JsonPropertyName("connector_artifact_sha256")] string? ConnectorArtifactSha256,
    [property: JsonPropertyName("connector_module_version_id")] string? ConnectorModuleVersionId,
    [property: JsonPropertyName("game_version")] string? GameVersion,
    [property: JsonPropertyName("game_commit")] string? GameCommit,
    [property: JsonPropertyName("modset_status")] string ModsetStatus,
    [property: JsonPropertyName("modset_fingerprint")] string ModsetFingerprint,
    [property: JsonPropertyName("loaded_mod_ids")] IReadOnlyList<string> LoadedModIds);

public sealed record PlatformLiveScore(
    string Name,
    double? Value,
    string DisplayValue,
    string Source);

public sealed record PlatformSelectedItem(
    string ReferentId,
    string Role,
    string Kind,
    string Label);

public sealed record PlatformReadView(
    string ReadId,
    string Kind,
    string Status);

public sealed record PlatformReceiptView(
    string Status,
    string Detail,
    string? RequestId);

public sealed record PlatformArtifactIdentity(
    string Product,
    string Version,
    string? SourceRevision,
    string? ModuleVersionId,
    string? ArtifactSha256);

public sealed record PlatformExactIdentity(
    PlatformArtifactIdentity? Game,
    PlatformArtifactIdentity? Connector,
    PlatformArtifactIdentity? Annotator,
    PlatformArtifactIdentity LiveUi,
    string? RuntimeInstanceId,
    string? EnvironmentFingerprint,
    string? HostKind,
    string? ModsetStatus,
    string? ModsetFingerprint,
    IReadOnlyList<string> LoadedModIds);

public sealed record PlatformLiveStatus(
    string Schema,
    DateTimeOffset ObservedAt,
    string TransportStatus,
    string? TransportDetail,
    string PolicyRuntimeTransportStatus,
    string? PolicyRuntimeTransportDetail,
    PolicyRuntimeStatus? PolicyRuntime,
    PlayerEnvironmentCapabilitiesResponse? Capabilities,
    PlayerEnvironmentSnapshot? Snapshot,
    PlayerEnvironmentControlSnapshot? Controller,
    RecordingApplicationStatus Recording,
    IReadOnlyList<PlatformLiveScore> Scores,
    IReadOnlyList<PlatformSelectedItem> Selected,
    PlatformReceiptView Receipt,
    IReadOnlyList<PlatformReadView> Reads,
    IReadOnlyList<string> Invalidations,
    PlatformExactIdentity ExactIdentity,
    IReadOnlyList<string> Errors)
{
    public const string CurrentSchema = "sts2.ai-platform/live-status-1";
}

public static class PlatformLiveStatusProjection
{
    public static PlatformLiveStatus Build(
        PolicyRuntimeStatus? policyRuntime,
        PlayerEnvironmentCapabilitiesResponse? capabilities,
        PlayerEnvironmentSnapshot? snapshot,
        PlayerEnvironmentControlSnapshot? controller,
        RecordingApplicationStatus recording,
        string transportStatus,
        string? transportDetail,
        string policyRuntimeTransportStatus,
        string? policyRuntimeTransportDetail,
        IReadOnlyList<string> errors)
    {
        if (capabilities is null || snapshot is null || controller is null)
        {
            if (capabilities is not null || snapshot is not null || controller is not null)
                throw new JsonException("Connector status merge requires a complete coherent response set.");
        }
        else
        {
            EnsureConnectorCoherence(capabilities, snapshot, controller);
        }

        return new PlatformLiveStatus(
            PlatformLiveStatus.CurrentSchema,
            DateTimeOffset.UtcNow,
            transportStatus,
            transportDetail,
            policyRuntimeTransportStatus,
            policyRuntimeTransportDetail,
            policyRuntime,
            capabilities,
            snapshot,
            controller,
            recording,
            ReadScores(policyRuntime),
            ReadSelected(policyRuntime),
            ReadReceipt(policyRuntime),
            ReadReads(snapshot, policyRuntime),
            ReadInvalidations(policyRuntime),
            BuildIdentity(capabilities, policyRuntime, recording),
            errors);
    }

    public static void EnsureConnectorCoherence(
        PlayerEnvironmentCapabilitiesResponse capabilities,
        PlayerEnvironmentSnapshot snapshot,
        PlayerEnvironmentControlSnapshot controller)
    {
        if (capabilities.Host is null
            || snapshot.Session is null
            || capabilities.ProtocolVersion != PlayerEnvironmentContract.ProtocolVersion
            || snapshot.ProtocolVersion != PlayerEnvironmentContract.ProtocolVersion
            || controller.ProtocolVersion != PlayerEnvironmentContract.ProtocolVersion
            || capabilities.SnapshotSchema != PlayerEnvironmentContract.SnapshotSchema
            || capabilities.ControlSchema != PlayerEnvironmentContract.ControlSchema
            || snapshot.Schema != PlayerEnvironmentContract.SnapshotSchema
            || controller.Schema != PlayerEnvironmentContract.ControlSchema)
        {
            throw new JsonException("Connector protocol or schema identity is incoherent.");
        }

        string capabilityRuntime = capabilities.Host.RuntimeInstanceId;
        string snapshotRuntime = snapshot.Session.RuntimeInstanceId;
        string controllerRuntime = controller.RuntimeInstanceId;
        string capabilityEnvironment = capabilities.EnvironmentFingerprint;
        string snapshotEnvironment = snapshot.Session.EnvironmentFingerprint;
        if (string.IsNullOrWhiteSpace(capabilityRuntime)
            || string.IsNullOrWhiteSpace(snapshotRuntime)
            || string.IsNullOrWhiteSpace(controllerRuntime)
            || string.IsNullOrWhiteSpace(capabilityEnvironment)
            || string.IsNullOrWhiteSpace(snapshotEnvironment)
            || !string.Equals(capabilityRuntime, snapshotRuntime, StringComparison.Ordinal)
            || !string.Equals(capabilityRuntime, controllerRuntime, StringComparison.Ordinal)
            || !string.Equals(capabilityEnvironment, snapshotEnvironment, StringComparison.Ordinal))
        {
            throw new JsonException("Connector runtime/environment coherence check failed.");
        }
    }

    private static PlatformExactIdentity BuildIdentity(
        PlayerEnvironmentCapabilitiesResponse? capabilities,
        PolicyRuntimeStatus? policyRuntime,
        RecordingApplicationStatus recording)
    {
        PlatformArtifactIdentity? connector = capabilities?.Host.Implementation is
            { } implementation
            ? new PlatformArtifactIdentity(
                "STS2_MCP",
                capabilities.Host.Version,
                implementation.SourceRevision,
                implementation.ModuleVersionId,
                implementation.ArtifactSha256)
            : null;
        PlatformArtifactIdentity? game = capabilities?.Game is { } gameIdentity
            ? new PlatformArtifactIdentity(
                "sts2",
                gameIdentity.Version ?? "unknown",
                gameIdentity.Commit,
                null,
                gameIdentity.MainAssemblyHash?.ToString())
            : null;
        PolicyRuntimeEnvironmentStatus? environment = policyRuntime?.Environment;
        connector ??= environment == null
            ? null
            : new PlatformArtifactIdentity(
                "STS2_MCP",
                environment.ConnectorVersion,
                environment.ConnectorSourceRevision,
                environment.ConnectorModuleVersionId,
                environment.ConnectorArtifactSha256);
        game ??= environment == null
            ? null
            : new PlatformArtifactIdentity(
                "sts2",
                environment.GameVersion ?? "unknown",
                environment.GameCommit,
                null,
                null);
        ExactArtifactIdentity? annotatorIdentity = recording.Environment?.Annotator;
        PlatformArtifactIdentity? annotator = annotatorIdentity == null
            ? null
            : new PlatformArtifactIdentity(
                annotatorIdentity.Product,
                annotatorIdentity.Version,
                annotatorIdentity.SourceRevision,
                annotatorIdentity.ModuleVersionId,
                annotatorIdentity.Sha256);
        return new PlatformExactIdentity(
            game,
            connector,
            annotator,
            PlatformLiveUiMod.CurrentArtifactIdentity(),
            capabilities?.Host.RuntimeInstanceId ?? environment?.RuntimeInstanceId,
            capabilities?.EnvironmentFingerprint ?? environment?.EnvironmentFingerprint,
            capabilities?.Host.HostKind ?? environment?.HostKind,
            capabilities?.Game.Modset.Status ?? environment?.ModsetStatus,
            capabilities?.Game.Modset.Fingerprint ?? environment?.ModsetFingerprint,
            capabilities?.Game.Modset.LoadedModIds ?? environment?.LoadedModIds ?? Array.Empty<string>());
    }

    private static IReadOnlyList<PlatformLiveScore> ReadScores(PolicyRuntimeStatus? policyRuntime)
    {
        PolicyRuntimeDecisionStatus? decision = policyRuntime?.LastDecision;
        if (decision == null)
        {
            return new[]
            {
                new PlatformLiveScore(
                    "scores",
                    null,
                    "unavailable",
                    "Policy Runtime status has no last_decision")
            };
        }

        return decision.Scores
            .Select((score, index) => new PlatformLiveScore(
                $"candidate[{index}]",
                score,
                score.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                "Policy Runtime status.last_decision.scores"))
            .ToArray();
    }

    private static IReadOnlyList<PlatformSelectedItem> ReadSelected(PolicyRuntimeStatus? policyRuntime)
    {
        PolicyRuntimeDecisionStatus? decision = policyRuntime?.LastDecision;
        if (decision?.SelectedIndex is not int selectedIndex)
            return Array.Empty<PlatformSelectedItem>();

        return new[]
        {
            new PlatformSelectedItem(
                decision.BoundActionId ?? $"candidate[{selectedIndex}]",
                "policy-selected",
                "runtime-decision",
                decision.BoundActionLabel ?? $"candidate[{selectedIndex}]")
        };
    }

    private static PlatformReceiptView ReadReceipt(PolicyRuntimeStatus? policyRuntime)
    {
        PolicyRuntimeReceiptStatus? receipt = policyRuntime?.LastReceipt;
        if (receipt == null)
        {
            return new PlatformReceiptView(
                "unavailable",
                "Policy Runtime status has no last_receipt.",
                null);
        }

        string detail = receipt.ReasonCode
            ?? (receipt.SuccessorSnapshotId == null
                ? "No reason code supplied."
                : $"successor={receipt.SuccessorSnapshotId}");
        return new PlatformReceiptView(receipt.Delivery, detail, receipt.RequestId);
    }

    private static IReadOnlyList<PlatformReadView> ReadReads(
        PlayerEnvironmentSnapshot? snapshot,
        PolicyRuntimeStatus? policyRuntime)
    {
        var reads = new Dictionary<string, PlatformReadView>(StringComparer.Ordinal);
        if (snapshot != null)
        {
            foreach (PlayerEnvironmentReadOpportunity read in snapshot.Reads)
            {
                reads[read.ReadId] = new PlatformReadView(
                    read.ReadId,
                    read.Kind,
                    "advertised by Connector Snapshot");
            }
        }
        if (policyRuntime != null)
        {
            foreach (PolicyRuntimeReadStatus read in policyRuntime.Reads)
            {
                reads[read.ReadId] = new PlatformReadView(
                    read.ReadId,
                    read.Kind,
                    "materialized by Policy Runtime");
            }
        }
        return reads.Values
            .OrderBy(read => read.Kind, StringComparer.Ordinal)
            .ThenBy(read => read.ReadId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadInvalidations(PolicyRuntimeStatus? policyRuntime) =>
        policyRuntime?.Invalidations.ToArray()
        ?? Array.Empty<string>();
}
