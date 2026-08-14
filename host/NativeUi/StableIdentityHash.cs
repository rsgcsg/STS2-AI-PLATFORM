using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace STS2Connector.NativeUi;

internal static class StableIdentityHash
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        WriteIndented = false
    };

    public static string Object(object value)
    {
        string json = JsonSerializer.Serialize(value, CanonicalOptions);
        return Text(json);
    }

    public static string Text(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
