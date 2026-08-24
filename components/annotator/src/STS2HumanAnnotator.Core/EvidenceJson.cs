using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STS2HumanAnnotator.Core;

public static class EvidenceJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public static JsonSerializerOptions IndentedOptions { get; } = new(Options)
    {
        WriteIndented = true
    };
}
