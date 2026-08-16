using STS2Connector.HostControl;

namespace STS2Connector.Host.Tests;

public sealed class HostLifecycleControlTests
{
    private const string Token =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void ProcessControlIsDisabledWithoutAnExplicitToken()
    {
        Assert.Null(HostLifecycleControl.ResolveConfiguredToken(null));
        Assert.Null(HostLifecycleControl.ResolveConfiguredToken("  "));
        Assert.Throws<InvalidOperationException>(
            () => HostLifecycleControl.ResolveConfiguredToken("short"));
        Assert.Throws<InvalidOperationException>(
            () => HostLifecycleControl.ResolveConfiguredToken(Token.ToUpperInvariant()));
    }

    [Fact]
    public void ShutdownRequiresBothSecretAndCurrentRuntimeIdentity()
    {
        Assert.Equal(
            "host_control_disabled",
            HostLifecycleControl.Authorize(null, Token, "runtime-a", "runtime-a").Status);
        Assert.Equal(
            "host_control_unauthorized",
            HostLifecycleControl.Authorize(Token, new string('0', 64), "runtime-a", "runtime-a").Status);
        Assert.Equal(
            "runtime_instance_changed",
            HostLifecycleControl.Authorize(Token, Token, "runtime-old", "runtime-a").Status);

        HostShutdownAuthorization allowed =
            HostLifecycleControl.Authorize(Token, Token, "runtime-a", "runtime-a");
        Assert.True(allowed.Allowed);
        Assert.Equal("shutdown_authorized", allowed.Status);
    }
}
