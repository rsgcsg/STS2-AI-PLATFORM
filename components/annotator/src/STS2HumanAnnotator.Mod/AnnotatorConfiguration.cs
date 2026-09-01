using System.Text.Json;

namespace STS2HumanAnnotator.Mod;

internal sealed record AnnotatorConfiguration(
    string RecordingRoot,
    string RuntimeStatusPath)
{
    internal const string FileName = "STS2_HUMAN_ANNOTATOR.conf";
    internal const string RecordingRootEnvironmentVariable =
        "STS2_HUMAN_ANNOTATOR_RECORDING_ROOT";
    internal const string RuntimeStatusEnvironmentVariable =
        "STS2_HUMAN_ANNOTATOR_STATUS_PATH";

    internal static AnnotatorConfiguration Load(string modDirectory)
    {
        string defaultRoot = Path.Combine(modDirectory, "recordings");
        string defaultStatus = Path.Combine(modDirectory, "STS2_HUMAN_ANNOTATOR.runtime.json");
        string root = defaultRoot;
        string status = defaultStatus;
        string configPath = Path.Combine(modDirectory, FileName);
        if (File.Exists(configPath))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath));
            JsonElement value;
            if (document.RootElement.TryGetProperty("recording_root", out value)
                && value.ValueKind == JsonValueKind.String)
                root = value.GetString() ?? root;
            if (document.RootElement.TryGetProperty("runtime_status_path", out value)
                && value.ValueKind == JsonValueKind.String)
                status = value.GetString() ?? status;
        }

        root = Environment.GetEnvironmentVariable(RecordingRootEnvironmentVariable) ?? root;
        status = Environment.GetEnvironmentVariable(RuntimeStatusEnvironmentVariable) ?? status;
        return new AnnotatorConfiguration(
            Path.GetFullPath(root),
            Path.GetFullPath(status));
    }
}
