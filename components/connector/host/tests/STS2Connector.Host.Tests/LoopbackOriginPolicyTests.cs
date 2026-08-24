namespace STS2Connector.Tests;

public sealed class LoopbackOriginPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://localhost:3000")]
    [InlineData("https://127.0.0.1:8443")]
    [InlineData("http://[::1]:3000")]
    public void AllowsNonBrowserAndLoopbackOrigins(string? origin)
    {
        Assert.True(LoopbackOriginPolicy.IsAllowed(origin));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("https://example.com")]
    [InlineData("file:///tmp/client.html")]
    [InlineData("not a uri")]
    public void RejectsNonLoopbackBrowserOrigins(string origin)
    {
        Assert.False(LoopbackOriginPolicy.IsAllowed(origin));
    }
}
