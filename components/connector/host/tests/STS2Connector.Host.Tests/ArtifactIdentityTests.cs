using System.Reflection;
using STS2Connector.Authority;

namespace STS2Connector.Host.Tests;

public sealed class ArtifactIdentityTests
{
    [Fact]
    public void InformationalVersionUsesTheConnectorSourceRevision()
    {
        Assembly assembly = typeof(ConnectorMod).Assembly;
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        Assert.NotNull(HostArtifactIdentity.SourceRevision);
        Assert.EndsWith($"+{HostArtifactIdentity.SourceRevision}", informationalVersion);
    }
}
