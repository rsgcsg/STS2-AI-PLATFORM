export const SUPPORTED_RUNTIMES = Object.freeze([Object.freeze({
  id: "darwin-arm64-v0.111.0-41cef1ea",
  platform: "darwin",
  architecture: "arm64",
  gameVersion: "v0.111.0",
  gameCommit: "41cef1ea",
  executableSha256: "ec8c10831dbb424c45859907f5ef6a7711f7a6e9a02f386ad13922ba8a7fcbe7",
  runtimeMainAssemblyHash: 1010476334,
  sts2AssemblySha256: "9cb4f1ad8c9f284aa8fec3122ffd6d780bbf543d875c817abdd12ff63fbf12b4",
  godotSharpAssemblySha256: "0e4897ecdfb31456a97c7d8028dfb8d7dbdc632e2f73fc9b438d7b266a139289"
})]);

export const EXPERIMENTAL_RUNTIMES = Object.freeze([Object.freeze({
  id: "win32-x64-v0.111.0-41cef1ea-candidate",
  platform: "win32",
  architecture: "x64",
  gameVersion: "v0.111.0",
  gameCommit: "41cef1ea",
  executableSha256: "8602c26bffd2937e3841835fd8360ef8e974624a543e05977229fd3d062be231",
  runtimeMainAssemblyHash: 222455745,
  sts2AssemblySha256: "0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9",
  godotSharpAssemblySha256: "0e4897ecdfb31456a97c7d8028dfb8d7dbdc632e2f73fc9b438d7b266a139289"
})]);

// Kept for source compatibility with the initial preview API.
export const SUPPORTED_RUNTIME = SUPPORTED_RUNTIMES[0];

const IDENTITY_FIELDS = Object.freeze([
  "platform",
  "architecture",
  "gameVersion",
  "gameCommit",
  "executableSha256",
  "runtimeMainAssemblyHash",
  "sts2AssemblySha256",
  "godotSharpAssemblySha256"
]);

function actualRuntimeIdentity(identity) {
  return {
    platform: identity?.platform ?? null,
    architecture: identity?.architecture ?? null,
    gameVersion: identity?.release?.version ?? null,
    gameCommit: identity?.release?.commit ?? null,
    executableSha256: identity?.executable?.sha256 ?? null,
    runtimeMainAssemblyHash: identity?.runtime_main_assembly_hash ?? null,
    sts2AssemblySha256: identity?.sts2_assembly?.sha256 ?? null,
    godotSharpAssemblySha256: identity?.godotsharp_assembly?.sha256 ?? null
  };
}

function compareRuntime(actual, expected) {
  return IDENTITY_FIELDS.filter((key) => actual[key] !== expected[key]);
}

export function evaluateRuntimeCompatibility(identity, {
  supported = SUPPORTED_RUNTIMES,
  experimental = EXPERIMENTAL_RUNTIMES
} = {}) {
  const actual = actualRuntimeIdentity(identity);
  for (const expected of supported) {
    const mismatches = compareRuntime(actual, expected);
    if (mismatches.length === 0) {
      return {
        status: "supported_exact",
        support_id: expected.id,
        mismatches,
        expected,
        actual
      };
    }
  }

  for (const expected of experimental) {
    const mismatches = compareRuntime(actual, expected);
    if (mismatches.length === 0) {
      return {
        status: "known_experimental",
        support_id: expected.id,
        mismatches,
        expected,
        actual
      };
    }
  }

  const candidates = [...supported, ...experimental]
    .map((expected) => ({ expected, mismatches: compareRuntime(actual, expected) }))
    .sort((left, right) => left.mismatches.length - right.mismatches.length);
  const nearest = candidates[0] ?? { expected: null, mismatches: [...IDENTITY_FIELDS] };
  return {
    status: "unsupported",
    support_id: nearest.expected?.id ?? null,
    mismatches: nearest.mismatches,
    expected: nearest.expected,
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
