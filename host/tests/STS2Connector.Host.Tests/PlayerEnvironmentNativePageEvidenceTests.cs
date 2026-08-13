using STS2Connector.LiveHost.Contracts;
using STS2Connector.Authority;
using STS2Connector.PlayerEnvironment.Protocol;
using STS2Connector.PlayerEnvironment;
using System.Text.Json;

namespace STS2Connector.Tests;

public sealed class PlayerEnvironmentNativePageEvidenceTests
{
    [Fact]
    public void EnvironmentIdentityOwnsTheProtocolNeutralHostIdentity()
    {
        LiveHostIdentity identity = EnvironmentIdentityRuntime.HostIdentity();

        Assert.Equal("sts2_live_player_environment_host", identity.Id);
        Assert.Equal("STS2 Live Player Environment Host", identity.Name);
        Assert.False(string.IsNullOrWhiteSpace(identity.ModuleVersionId));
        Assert.False(string.IsNullOrWhiteSpace(identity.RuntimeInstanceId));
    }

    [Fact]
    public void MutationControlUsesTheProtocolNeutralAuthorityContract()
    {
        MutationControlSnapshot snapshot = MutationControlRuntime.Snapshot();

        Assert.Equal(MutationControlContract.ProtocolVersion, snapshot.ProtocolVersion);
        Assert.Equal(
            EnvironmentIdentityRuntime.HostIdentity().RuntimeInstanceId,
            snapshot.RuntimeInstanceId);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.RuntimeInstanceId));
    }

    [Fact]
    public void DisabledAndStaleProfilesFailClosedBeforeOpeningNativeUi()
    {
        var environment = new FakePlayerEnvironment();
        var machine = new PlayerEnvironmentNativePageSession(environment);
        PlayerEnvironmentNativePageOpenRequest request = Request();

        PlayerEnvironmentNativePageOperationResult disabled = machine.Open(request);
        Assert.Equal("native_page_evidence_disabled", disabled.ErrorCode);
        Assert.Equal(0, environment.OpenCount);

        machine.Configure(enabled: true);
        PlayerEnvironmentNativePageOperationResult stale = machine.Open(
            request with { ExpectedSnapshotId = "state-stale" });
        Assert.Equal("stale_state", stale.ErrorCode);
        Assert.Equal(0, environment.OpenCount);

        PlayerEnvironmentNativePageOperationResult runtime = machine.Open(
            request with { ExpectedRuntimeInstanceId = "runtime-stale" });
        Assert.Equal("runtime_instance_changed", runtime.ErrorCode);
        Assert.Equal(0, environment.OpenCount);
    }

    [Fact]
    public void OpenReadReturnIsRuntimeBoundAndNeverAuthorizesMutation()
    {
        var environment = new FakePlayerEnvironment();
        var machine = new PlayerEnvironmentNativePageSession(environment);
        machine.Configure(enabled: true);

        PlayerEnvironmentNativePageResponse opened =
            Assert.IsType<PlayerEnvironmentNativePageResponse>(
                machine.Open(Request()).Response);
        Assert.Equal("open", opened.Phase);
        Assert.False(opened.CreatesMutationAuthority);
        Assert.False(opened.EntersActionLedger);
        Assert.True(machine.ReservesInputOwner);
        Assert.Equal("run_deck", opened.Page?.ReadKind);

        PlayerEnvironmentNativePageOperationResult wrongRuntime = machine.Read(
            opened.SessionId,
            "runtime-other");
        Assert.Equal("runtime_instance_changed", wrongRuntime.ErrorCode);

        PlayerEnvironmentNativePageResponse read =
            Assert.IsType<PlayerEnvironmentNativePageResponse>(
                machine.Read(opened.SessionId, "runtime-a").Response);
        Assert.Equal("open", read.Phase);
        Assert.Equal(1, environment.ReadCount);

        PlayerEnvironmentNativePageResponse returned =
            Assert.IsType<PlayerEnvironmentNativePageResponse>(
                machine.Return(
                    opened.SessionId,
                    new PlayerEnvironmentNativePageReturnRequest(
                        PlayerEnvironmentContract.NativePageEvidenceProfile,
                        "runtime-a")).Response);
        Assert.Equal("returned", returned.Phase);
        Assert.Equal("state-a", returned.PostSnapshotId);
        Assert.Equal(returned.PreOwner, returned.PostOwner);
        Assert.False(machine.ReservesInputOwner);
        Assert.Equal(1, environment.ResetCount);
    }

    [Fact]
    public void FailedReturnKeepsInputReservedUntilExactRecoverySucceeds()
    {
        var environment = new FakePlayerEnvironment
        {
            FailFirstReturn = true
        };
        var machine = new PlayerEnvironmentNativePageSession(environment);
        machine.Configure(enabled: true);
        PlayerEnvironmentNativePageResponse opened = machine.Open(Request()).Response!;
        var request = new PlayerEnvironmentNativePageReturnRequest(
            PlayerEnvironmentContract.NativePageEvidenceProfile,
            "runtime-a");

        PlayerEnvironmentNativePageResponse blocked =
            machine.Return(opened.SessionId, request).Response!;
        Assert.Equal("recovery_required", blocked.Phase);
        Assert.Equal("native_return_control_unavailable", blocked.ErrorCode);
        Assert.True(machine.ReservesInputOwner);

        PlayerEnvironmentNativePageResponse recovered =
            machine.Return(opened.SessionId, request).Response!;
        Assert.Equal("returned", recovered.Phase);
        Assert.False(machine.ReservesInputOwner);
        Assert.Equal(2, environment.ReturnCount);
    }

    [Fact]
    public void PartialOpenFailureKeepsInputReservedUntilExactRecoverySucceeds()
    {
        var environment = new FakePlayerEnvironment
        {
            FailOpenWithOwnedPage = true
        };
        var machine = new PlayerEnvironmentNativePageSession(environment);
        machine.Configure(enabled: true);

        PlayerEnvironmentNativePageResponse blocked = machine.Open(Request()).Response!;
        Assert.Equal("recovery_required", blocked.Phase);
        Assert.Equal("native_page_recovery_required", blocked.ErrorCode);
        Assert.True(machine.ReservesInputOwner);

        PlayerEnvironmentNativePageResponse recovered = machine.Return(
            blocked.SessionId,
            new PlayerEnvironmentNativePageReturnRequest(
                PlayerEnvironmentContract.NativePageEvidenceProfile,
                "runtime-a")).Response!;
        Assert.Equal("returned", recovered.Phase);
        Assert.False(machine.ReservesInputOwner);
    }

    [Fact]
    public void PostOpenVerificationFailureRequiresRestorationPollBeforeRelease()
    {
        var environment = new FakePlayerEnvironment
        {
            ThrowOnPostOpenCapture = true
        };
        var machine = new PlayerEnvironmentNativePageSession(environment);
        machine.Configure(enabled: true);

        PlayerEnvironmentNativePageResponse blocked = machine.Open(Request()).Response!;
        Assert.Equal("recovery_required", blocked.Phase);
        Assert.True(machine.ReservesInputOwner);
        Assert.Equal(1, environment.ReturnCount);

        PlayerEnvironmentNativePageResponse recovered = machine.Read(
            blocked.SessionId,
            "runtime-a").Response!;
        Assert.Equal("returned", recovered.Phase);
        Assert.False(machine.ReservesInputOwner);
    }

    private static PlayerEnvironmentNativePageOpenRequest Request() => new(
        PlayerEnvironmentContract.NativePageEvidenceProfile,
        PlayerEnvironmentNativePageSession.RunDeckKind,
        "state-a",
        "runtime-a");

    private sealed class FakePlayerEnvironment :
        IPlayerEnvironmentNativePageHost
    {
        private static readonly PlayerEnvironmentNativePageOwner PreOwner =
            new("map", "map_navigation", "semantic:map:map_navigation");
        private static readonly PlayerEnvironmentNativePageOwner PageOwner =
            new("map", "unsupported", "capstone:deck:page-a");
        private PlayerEnvironmentNativePageRuntimeSnapshot _snapshot =
            Snapshot("state-a", PreOwner);

        public int OpenCount { get; private set; }
        public int ReadCount { get; private set; }
        public int ReturnCount { get; private set; }
        public int ResetCount { get; private set; }
        public bool FailFirstReturn { get; init; }
        public bool FailOpenWithOwnedPage { get; init; }
        public bool ThrowOnPostOpenCapture { get; init; }
        public bool HasOwnedPage { get; private set; }
        private bool _throwNextCapture;

        public PlayerEnvironmentNativePageRuntimeSnapshot Capture()
        {
            if (_throwNextCapture)
            {
                _throwNextCapture = false;
                throw new InvalidOperationException("fixture post-open capture failure");
            }
            return _snapshot;
        }

        public PlayerEnvironmentNativePageResult Open(
            string kind,
            PlayerEnvironmentNativePageRuntimeSnapshot pre)
        {
            OpenCount++;
            HasOwnedPage = true;
            _snapshot = Snapshot("state-page", PageOwner);
            _throwNextCapture = ThrowOnPostOpenCapture;
            if (FailOpenWithOwnedPage)
            {
                return PlayerEnvironmentNativePageResult.Failure(
                    "native_page_recovery_required",
                    "fixture partial open failure");
            }
            return PlayerEnvironmentNativePageResult.Success(Page());
        }

        public PlayerEnvironmentNativePageResult Read(string kind)
        {
            ReadCount++;
            return PlayerEnvironmentNativePageResult.Success(Page());
        }

        public PlayerEnvironmentNativePageResult Return(string kind)
        {
            ReturnCount++;
            if (FailFirstReturn && ReturnCount == 1)
            {
                return PlayerEnvironmentNativePageResult.Failure(
                    "native_return_control_unavailable",
                    "fixture failure");
            }
            HasOwnedPage = false;
            _snapshot = Snapshot("state-a", PreOwner);
            return new PlayerEnvironmentNativePageResult(null, null, null);
        }

        public void Reset()
        {
            HasOwnedPage = false;
            ResetCount++;
        }

        private static PlayerEnvironmentNativePageRuntimeSnapshot Snapshot(
            string state,
            PlayerEnvironmentNativePageOwner owner) => new(
                state,
                "runtime-a",
                owner,
                null!,
                null!,
                new[] { STS2Connector.LiveHost.PlayerVisibleReadBuilder.RunDeckKind });

        private static PlayerEnvironmentNativePageRead Page() => new(
            "fixture.deck.page",
            "run_deck",
            0,
            Array.Empty<string>(),
            "sts2.he/read.run-deck-1",
            JsonSerializer.SerializeToNode(
                new RunDeckReadContent(
                    "run_deck",
                    0,
                    Array.Empty<VisibleCard>()))!,
          