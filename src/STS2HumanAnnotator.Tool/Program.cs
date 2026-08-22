using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using STS2HumanAnnotator.Core;

return args switch
{
    ["audit", string directory] => Audit(directory),
    ["export", string directory, string output] => Export(directory, output),
    ["identity", string assembly] => Identity(assembly),
    _ => Usage()
};

static int Audit(string directory)
{
    RecordingAuditResult audit = RecordingAuditor.Audit(directory);
    Console.WriteLine(JsonSerializer.Serialize(audit, EvidenceJson.IndentedOptions));
    return audit.Status == "pass" ? 0 : 1;
}

static int Export(string directory, string output)
{
    long count = RecordingAuditor.ExportAdmitted(directory, output);
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
    Console.Error.WriteLine("usage: sts2-human-annotator audit <recording-dir> | export <recording-dir> <output.jsonl> | identity <assembly>");
    return 2;
}
