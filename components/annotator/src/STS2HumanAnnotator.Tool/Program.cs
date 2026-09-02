using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using STS2HumanAnnotator.Core;

return args switch
{
    ["audit", string directory] => Audit(directory),
    ["audit-native-semantic", string directory] => AuditNativeSemantic(directory),
    ["export", string directory, string output] => Export(directory, output),
    ["pack-session", string directory, string worker, string campaign,
        string output, string sourceRevision, "human_origin_attested"] =>
        PackSession(directory, worker, campaign, output, sourceRevision),
    ["identity", string assembly] => Identity(assembly),
    _ => Usage()
};

static int PackSession(
    string directory,
    string worker,
    string campaign,
    string output,
    string sourceRevision)
{
    SessionBundleResult result = SessionBundlePacker.Pack(
        directory,
        worker,
        campaign,
        output,
        sourceRevision,
        humanOriginAttested: true);
    Console.WriteLine(JsonSerializer.Serialize(result, EvidenceJson.IndentedOptions));
    return 0;
}

static int Audit(string directory)
{
    RecordingAuditResult audit = RecordingSessionAuditor.Audit(directory);
    Console.WriteLine(JsonSerializer.Serialize(audit, EvidenceJson.IndentedOptions));
    return audit.Status == "pass" ? 0 : 1;
}

static int AuditNativeSemantic(string directory)
{
    string path = Path.Combine(
        Path.GetFullPath(directory),
        "native-semantic-discriminator.jsonl");
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"Native semantic discriminator stream is absent: {path}");
        return 1;
    }
    NativeSemanticDiscriminatorEvent[] events = File.ReadLines(path)
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line => JsonSerializer.Deserialize<NativeSemanticDiscriminatorEvent>(
            line,
            EvidenceJson.Options)
            ?? throw new InvalidDataException("A discriminator event could not be decoded."))
        .ToArray();
    NativeSemanticDiscriminatorReport report =
        NativeSemanticDiscriminatorAnalyzer.Analyze(events);
    Console.WriteLine(JsonSerializer.Serialize(report, EvidenceJson.IndentedOptions));
    return report.Status == "pass" ? 0 : 1;
}

static int Export(string directory, string output)
{
    long count = RecordingSessionAuditor.ExportAdmitted(directory, output);
    Console.WriteLine(JsonSerializer.Serialize(
        new { status = "pass", exported_records = count, output = Path.GetFullPath(output) },
        EvidenceJson.IndentedOptions));
    return 0;
}

static int Identity(string assembly)
{
    string path = Path.GetFullPath(assembly);
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    MetadataReader metadata = pe.GetMetadataReader();
    Guid mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
    Console.WriteLine(JsonSerializer.Serialize(
        new
        {
            path,
            sha256 = EvidenceIdentity.Sha256File(path),
            module_version_id = mvid.ToString("D")
        },
        EvidenceJson.IndentedOptions));
    return 0;
}

static int Usage()
{
    Console.Error.WriteLine("usage: sts2-human-annotator audit <recording-dir> | audit-native-semantic <recording-dir> | export <recording-dir> <output.jsonl> | pack-session <recording-dir> <worker-id> <campaign-id> <output-dir> <source-revision> human_origin_attested | identity <assembly>");
    return 2;
}
