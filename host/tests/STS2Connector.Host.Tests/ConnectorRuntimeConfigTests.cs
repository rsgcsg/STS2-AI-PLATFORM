namespace STS2Connector;

public sealed class ConnectorRuntimeConfigTests
{
    [Fact]
    public void UsesFilePortWhenProcessOverrideIsAbsent()
    {
        Assert.Equal(16000, ConnectorMod.ResolveProcessPort(16000, null));
        Assert.Equal(16000, ConnectorMod.ResolveProcessPort(16000, ""));
        Assert.Equal(16000, ConnectorMod.ResolveProcessPort(16000, "   "));
    }

    [Fact]
    public void ProcessPortOverridesSharedFileConfiguration()
    {
        Assert.Equal(17001, ConnectorMod.ResolveProcessPort(15526, "17001"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    public void InvalidProcessPortFailsClosed(string value)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ConnectorMod.ResolveProcessPort(15526, value));
        Assert.Contains(ConnectorMod.PortEnvironmentVariable, exception.Message);
    }
}
