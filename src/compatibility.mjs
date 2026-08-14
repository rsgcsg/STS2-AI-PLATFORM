export const SUPPORTED_RUNTIME = Object.freeze({
  id: "darwin-arm64-v0.111.0-41cef1ea",
  platform: "darwin",
  architecture: "arm64",
  gameVersion: "v0.111.0",
  gameCommit: "41cef1ea",
  executableSha256: "ec8c10831dbb424c45859907f5ef6a7711f7a6e9a02f386ad13922ba8a7fcbe7",
  runtimeMainAssemblyHash: 1010476334,
  sts2AssemblySha256: "9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4",
  godotSharpAssemblySha256: "0e4897ecdfb31456a97c7d8028dfb8d7dbdc632e2f73fc9b438d7b266a139289"
});

export function evaluateRuntimeCompatibility(identity, expected = SUPPORTED_RUNTIME) {
  const actual = {
    platform: identity?.platform ?? null,
    architecture: identity?.architecture ?? null,
    gameVersion: identity?.release?.version ?? null,
    gameCommit: identity?.release?.commit ?? null,
    executableSha256: identity?.executable?.sha256 ?? null,
    runtimeMainAssemblyHash: identity?.runtime_main_assembly_hash ?? null,
    sts2AssemblySha256: identity?.sts2_assembly?.sha256 ?? null,
    godotSharpAssemblySha256: identity?.godotsharp_assembly?.sha256 ?? null
  };
  const mismatches = [];
  for (const key of [
    "platform",
    "architecture",
    "gameVersion",
    "gameCommit",
    "executableSha256",
    "runtimeMainAssemblyHash",
    "sts2AssemblySha256",
    "godotSharpAssemblySha256"
  ]) {
    if (actual[key] !== expected[key]) mismatches.push(key);
  }
  return {
    status: mismatches.length === 0 ? "supported_exact" : "unsupported",
    support_id: expected.id,
    mismatches,
    expected,
    actual
  };
}

export function requireSupportedRuntime(identity) {
  const compatibility = evaluateRuntimeCompatibility(identity);
  if (compatibility.status !== "supported_exact") {
    throw new Error(
      `Unsupported STS2 runtime (${compatibility.mismatches.join(", ")}); `
      + "run `npm run doctor` and use an explicit experimental probe rather than normal start."
    );
  }
  return compatibility;
}
