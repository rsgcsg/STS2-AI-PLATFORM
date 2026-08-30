import assert from "node:assert/strict";
import test from "node:test";
import {
  commandMatchesExecutable,
  fallbackInstallation,
  normalizeExactModsetCanary,
  normalizeInstalledProvenance,
  prepareExactWindowsModSettings,
  prepareSoleWindowsModSettings,
  resolveConnectorCanaryEnvironment,
  resolveWorkstationInstallation
} from "./workstation-platform.mjs";

test("Windows live settings preserve both artifacts but enable only Connector", () => {
  const settings = {
    schema_version: 8,
    mod_settings: {
      mods_enabled: true,
      mod_list: [
        { id: "STS2_MCP", is_enabled: true, source: "mods_directory" },
        { id: "STS2_HUMAN_ANNOTATOR", is_enabled: true, source: "mods_directory" },
        { id: "DISABLED_MOD", is_enabled: false, source: "mods_directory" }
      ]
    }
  };
  const result = prepareExactWindowsModSettings({
    settings,
    enabledModIds: ["STS2_MCP"],
    allowedPreviouslyEnabledModIds: ["STS2_MCP", "STS2_HUMAN_ANNOTATOR"]
  });
  assert.deepEqual(
    result.mod_settings.mod_list.map(({ id, is_enabled }) => ({ id, is_enabled })),
    [
      { id: "STS2_MCP", is_enabled: true },
      { id: "STS2_HUMAN_ANNOTATOR", is_enabled: false },
      { id: "DISABLED_MOD", is_enabled: false }
    ]
  );
  assert.equal(settings.mod_settings.mod_list[1].is_enabled, true);
});

test("Windows live settings reject an enabled third-party Mod", () => {
  const settings = {
    mod_settings: {
      mods_enabled: true,
      mod_list: [{ id: "GAMEPLAY_MOD", is_enabled: true, source: "mods_directory" }]
    }
  };
  assert.throws(() => prepareExactWindowsModSettings({
    settings,
    enabledModIds: ["STS2_MCP"],
    allowedPreviouslyEnabledModIds: ["STS2_MCP", "STS2_HUMAN_ANNOTATOR"]
  }), /non-admitted Modset/u);
});

test("Windows production settings preserve entries but enable only unified Platform", () => {
  const settings = {
    schema_version: 8,
    mod_settings: {
      mods_enabled: true,
      mod_list: [
        { id: "STS2-RitsuLib", is_enabled: true, source: "workshop", custom: "retained" },
        { id: "STS2_PLATFORM", is_enabled: false, source: "mods_directory" },
        { id: "CombatSolver", is_enabled: true, source: "workshop" }
      ]
    }
  };

  const result = prepareSoleWindowsModSettings({
    settings,
    enabledModId: "STS2_PLATFORM"
  });

  assert.deepEqual(
    result.mod_settings.mod_list.map(({ id, is_enabled, source }) => ({ id, is_enabled, source })),
    [
      { id: "STS2_PLATFORM", is_enabled: true, source: "mods_directory" },
      { id: "STS2-RitsuLib", is_enabled: false, source: "workshop" },
      { id: "CombatSolver", is_enabled: false, source: "workshop" }
    ]
  );
  assert.equal(result.mod_settings.mod_list[1].custom, "retained");
  assert.equal(settings.mod_settings.mod_list[0].is_enabled, true);
});

test("Windows installation uses Headless discovery and exact native paths", () => {
  const calls = [];
  const headlessApi = {
    source: "platform_host_runtime",
    discoverGameDirectory(options) {
      calls.push(options);
      return "E:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2";
    },
    resolveInstallation(directory, options) {
      return fallbackInstallation(directory, options);
    }
  };
  const result = resolveWorkstationInstallation({
    headlessApi,
    env: {},
    platform: "win32",
    arch: "x64",
    home: "C:\\Users\\player"
  });

  assert.equal(calls.length, 1);
  assert.equal(result.discovery_method, "platform_host_runtime");
  assert.equal(result.executable, "E:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2\\SlayTheSpire2.exe");
  assert.equal(result.data_dir, "E:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2\\data_sts2_windows_x86_64");
  assert.equal(result.mods_dir, "E:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2\\mods");
});

test("macOS fallback retains the established application bundle layout", () => {
  const result = resolveWorkstationInstallation({
    headlessApi: null,
    env: {},
    platform: "darwin",
    arch: "arm64",
    home: "/Users/player"
  });

  assert.equal(result.discovery_method, "platform_default");
  assert.equal(
    result.executable,
    "/Users/player/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2"
  );
  assert.equal(
    result.data_dir,
    "/Users/player/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources/data_sts2_macos_arm64"
  );
});

test("Windows Connector canaries bind exact game and source identities", () => {
  const compatibility = {
    canary_environment_variable: "STS2_CONNECTOR_EXPERIMENTAL_GAME_ID",
    artifact_canary_environment_variable: "STS2_CONNECTOR_EXPERIMENTAL_SOURCE_REVISION",
    runtimes: [{
      id: "win32-candidate",
      status: "candidate_exact",
      platform: "win32",
      architecture: "x64",
      game_version: "v0.111.0",
      game_commit: "41cef1ea",
      runtime_main_assembly_hash: 222455745,
      main_assembly_sha256: "1".repeat(64),
      main_assembly_mvid: "11111111-1111-1111-1111-111111111111"
    }]
  };
  const result = resolveConnectorCanaryEnvironment({
    compatibility,
    connectorBuild: { source_revision: "a".repeat(40) },
    gameRelease: {
      version: "v0.111.0",
      commit: "41cef1ea",
      main_assembly_hash: 222455745
    },
    gameIdentity: {
      sha256: "1".repeat(64),
      module_version_id: "11111111-1111-1111-1111-111111111111"
    },
    platform: "win32",
    architecture: "x64"
  });

  assert.deepEqual(result.environment, {
    STS2_CONNECTOR_EXPERIMENTAL_GAME_ID: "win32-candidate",
    STS2_CONNECTOR_EXPERIMENTAL_SOURCE_REVISION: "a".repeat(40)
  });
  assert.throws(
    () => resolveConnectorCanaryEnvironment({
      compatibility,
      connectorBuild: { source_revision: "a".repeat(40) },
      gameRelease: { version: "drift", commit: "41cef1ea", main_assembly_hash: 222455745 },
      gameIdentity: {
        sha256: "1".repeat(64),
        module_version_id: "11111111-1111-1111-1111-111111111111"
      },
      platform: "win32",
      architecture: "x64"
    }),
    /absent from Connector compatibility/u
  );
});

test("macOS supported-exact launch separates release-declared and runtime assembly hashes", () => {
  const compatibility = {
    canary_environment_variable: "STS2_CONNECTOR_EXPERIMENTAL_GAME_ID",
    artifact_canary_environment_variable: "STS2_CONNECTOR_EXPERIMENTAL_SOURCE_REVISION",
    runtimes: [{
      id: "darwin-supported",
      status: "supported_exact",
      platform: "darwin",
      architecture: "arm64",
      game_version: "v0.111.0",
      game_commit: "41cef1ea",
      runtime_main_assembly_hash: 1010476334,
      main_assembly_sha256: "2".repeat(64),
      main_assembly_mvid: "22222222-2222-2222-2222-222222222222"
    }]
  };
  const result = resolveConnectorCanaryEnvironment({
    compatibility,
    connectorBuild: { source_revision: "b".repeat(40) },
    gameRelease: {
      version: "v0.111.0",
      commit: "41cef1ea",
      main_assembly_hash: 1172974615
    },
    gameIdentity: {
      sha256: "2".repeat(64),
      module_version_id: "22222222-2222-2222-2222-222222222222"
    },
    platform: "darwin",
    architecture: "arm64"
  });

  assert.deepEqual(result.environment, {
    STS2_CONNECTOR_EXPERIMENTAL_SOURCE_REVISION: "b".repeat(40)
  });
  assert.throws(
    () => resolveConnectorCanaryEnvironment({
      compatibility,
      connectorBuild: { source_revision: "b".repeat(40) },
      gameRelease: {
        version: "v0.111.0",
        commit: "41cef1ea",
        main_assembly_hash: 1172974615
      },
      gameIdentity: {
        sha256: "3".repeat(64),
        module_version_id: "22222222-2222-2222-2222-222222222222"
      },
      platform: "darwin",
      architecture: "arm64"
    }),
    /absent from Connector compatibility/u
  );
});

test("process command matching binds the exact executable path", () => {
  assert.equal(commandMatchesExecutable(
    '"E:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2\\SlayTheSpire2.exe"',
    "E:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2\\SlayTheSpire2.exe"
  ), true);
  assert.equal(commandMatchesExecutable(
    '"E:\\Other\\SlayTheSpire2.exe"',
    "E:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2\\SlayTheSpire2.exe"
  ), false);
});

test("legacy macOS provenance is upgraded only through exact current identities", () => {
  const gameIdentity = {
    sha256: "1".repeat(64),
    module_version_id: "11111111-1111-1111-1111-111111111111"
  };
  const connectorArtifact = {
    sha256: "2".repeat(64),
    module_version_id: "22222222-2222-2222-2222-222222222222"
  };
  const currentGame = {
    release: { version: "v0.111.0", commit: "41cef1ea" },
    executable: { sha256: "3".repeat(64) },
    sts2_identity: gameIdentity
  };
  const connectorBuild = {
    source_revision: "a".repeat(40),
    player_environment_source_digest: "b".repeat(64),
    artifact_sha256: connectorArtifact.sha256,
    artifact_mvid: connectorArtifact.module_version_id
  };
  const legacy = {
    schema_version: 1,
    game: { sts2_identity: gameIdentity },
    connector_artifact: connectorArtifact
  };
  const result = normalizeInstalledProvenance({
    provenance: legacy,
    currentGame,
    connectorBuild,
    platform: "darwin",
    architecture: "arm64"
  });
  assert.equal(result.compatibility, "legacy_macos_v1_derived_exact");
  assert.deepEqual(result.provenance.game.release, currentGame.release);
  assert.deepEqual(result.provenance.connector_build, connectorBuild);
  assert.throws(
    () => normalizeInstalledProvenance({
      provenance: legacy,
      currentGame: {
        ...currentGame,
        sts2_identity: { ...gameIdentity, sha256: "4".repeat(64) }
      },
      connectorBuild,
      platform: "darwin",
      architecture: "arm64"
    }),
    /no longer matches the exact STS2 assembly/u
  );
  assert.throws(
    () => normalizeInstalledProvenance({
      provenance: legacy,
      currentGame,
      connectorBuild,
      platform: "win32",
      architecture: "x64"
    }),
    /different platform or architecture/u
  );
});

test("legacy macOS Modset canary is upgraded only inside the exact installed envelope", () => {
  const installed = {
    provenanceCompatibility: "legacy_macos_v1_derived_exact",
    provenance: {
      source_revision: "c".repeat(40),
      source_digest_sha256: "d".repeat(64),
      connector_build: { source_revision: "a".repeat(40) }
    },
    installedConnector: { sha256: "1".repeat(64), module_version_id: "connector-mvid" },
    installedAnnotator: { sha256: "2".repeat(64), module_version_id: "annotator-mvid" },
    currentGame: {
      release: { version: "v0.111.0", commit: "41cef1ea" },
      executable: { sha256: "3".repeat(64) },
      sts2_identity: { sha256: "4".repeat(64), module_version_id: "game-mvid" }
    }
  };
  const normalized = normalizeExactModsetCanary({
    canary: {
      connector_source_revision: "a".repeat(40),
      modset_fingerprint: "b".repeat(64)
    },
    installed,
    connectorRuntime: { status: "supported_exact", id: "mac" },
    platform: "darwin"
  });
  assert.equal(normalized.schema_version, 2);
  assert.equal(normalized.connector_game_id, null);
  assert.deepEqual(normalized.annotator_artifact, installed.installedAnnotator);
  assert.throws(
    () => normalizeExactModsetCanary({
      canary: {
        connector_source_revision: "e".repeat(40),
        modset_fingerprint: "b".repeat(64)
      },
      installed,
      connectorRuntime: { status: "supported_exact", id: "mac" },
      platform: "darwin"
    }),
    /schema is unsupported/u
  );
});
