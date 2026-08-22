using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace STS2HumanAnnotator.Core;

public static class EvidenceIdentity
{
    public static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string Sha256Json<T>(T value)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, EvidenceJson.Options);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static string Sha256Text(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
