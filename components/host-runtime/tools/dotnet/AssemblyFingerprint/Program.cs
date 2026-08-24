using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 2 || args[0] != "--assembly")
{
    Console.Error.WriteLine("Usage: AssemblyFingerprint --assembly <path>");
    return 2;
}

var assemblyPath = Path.GetFullPath(args[1]);
var fileInfo = new FileInfo(assemblyPath);
await using var hashStream = File.OpenRead(assemblyPath);
var assemblySha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(hashStream));
await using var stream = File.OpenRead(assemblyPath);
using var pe = new PEReader(stream, PEStreamOptions.PrefetchEntireImage);
if (!pe.HasMetadata)
{
    Console.Error.WriteLine("The input is not a managed assembly.");
    return 3;
}

var metadata = pe.GetMetadataReader();
var module = metadata.GetModuleDefinition();
var inventory = new List<object>();
var methodCount = 0;
var methodBodyCount = 0;

foreach (var typeHandle in metadata.TypeDefinitions)
{
    var type = metadata.GetTypeDefinition(typeHandle);
    var typeNamespace = metadata.GetString(type.Namespace);
    var typeName = metadata.GetString(type.Name);
    var methods = new List<object>();
    foreach (var methodHandle in type.GetMethods())
    {
        methodCount += 1;
        var method = metadata.GetMethodDefinition(methodHandle);
        string? ilSha256 = null;
        var ilSize = 0;
        if (method.RelativeVirtualAddress != 0)
        {
            var body = pe.GetMethodBody(method.RelativeVirtualAddress);
            var il = body.GetILBytes() ?? Array.Empty<byte>();
            ilSize = il.Length;
            ilSha256 = Convert.ToHexStringLower(SHA256.HashData(il));
            methodBodyCount += 1;
        }
        methods.Add(new
        {
            name = metadata.GetString(method.Name),
            attributes = method.Attributes.ToString(),
            signature_sha256 = Convert.ToHexStringLower(
                SHA256.HashData(metadata.GetBlobBytes(method.Signature))),
            il_size = ilSize,
            il_sha256 = ilSha256
        });
    }
    inventory.Add(new
    {
        @namespace = typeNamespace,
        name = typeName,
        attributes = type.Attributes.ToString(),
        methods
    });
}

var report = new
{
    schema_version = 1,
    generated_at = DateTimeOffset.UtcNow,
    assembly = new
    {
        file_name = Path.GetFileName(assemblyPath),
        size = fileInfo.Length,
        sha256 = assemblySha256,
        module_mvid = metadata.GetGuid(module.Mvid).ToString("D"),
        metadata_version = metadata.MetadataVersion,
        type_count = metadata.TypeDefinitions.Count,
        method_count = methodCount,
        method_body_count = methodBodyCount
    },
    inventory
};

Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
}));
return 0;
