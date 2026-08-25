using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace STS2Connector.Authority;

/// <summary>
/// Identifies the loaded Player Environment Host artifact without disclosing
/// its local path.
/// A missing digest is an identity failure, never a compatibility hint.
/// </summary>
internal static class HostArtifactIdentity
{
    private static readonly Lazy<string?> LoadedDigest = new(ReadLoadedAssemblySha256);
    private static readonly Lazy<string?> EmbeddedSourceRevision = new(ReadSourceRevision);

    public static string? LoadedAssemblySha256 => LoadedDigest.Value;
    public static string? SourceRevision => EmbeddedSourceRevision.Value;
    public static string LoadedAssemblyMvid =>
        typeof(ConnectorMod).Assembly.ManifestModule.ModuleVersionId.ToString("D");

    internal static string? HashFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            byte[] digest = SHA256.HashData(File.ReadAllBytes(path));
            return Convert.ToHexString(digest).ToLowerInvariant();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ReadLoadedAssemblySha256() =>
        HashFile(typeof(ConnectorMod).Assembly.Location);

    private static string? ReadSourceRevision()
    {
        AssemblyMetadataAttribute[] metadata = typeof(ConnectorMod).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToArray();
        string? revision = metadata.FirstOrDefault(attribute =>
                string.Equals(attribute.Key, "ConnectorSourceRevision", StringComparison.Ordinal))
            ?.Value
            ?? metadata.FirstOrDefault(attribute =>
                string.Equals(attribute.Key, "SourceRevision", StringComparison.Ordinal))
            ?.Value;
        return string.IsNullOrWhiteSpace(revision) ? null : revision;
    }
}
