using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: STS2.ArtifactIdentity <managed-assembly>");
    return 64;
}

string file = Path.GetFullPath(args[0]);
using FileStream stream = File.OpenRead(file);
using var peReader = new PEReader(stream);
if (!peReader.HasMetadata)
{
    Console.Error.WriteLine("The file is not a managed assembly.");
    return 65;
}

MetadataReader metadata = peReader.GetMetadataReader();
Guid mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
stream.Position = 0;
string sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
Console.WriteLine(JsonSerializer.Serialize(new
{
    schema_version = 1,
    sha256,
    module_version_id = mvid.ToString(),
    file_name = Path.GetFileName(file)
}));
return 0;
