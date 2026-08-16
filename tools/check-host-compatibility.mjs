#!/usr/bin/env node
import { readFileSync } from "node:fs";
import path from "node:path";

const root = process.cwd();
const contract = JSON.parse(readFileSync(path.join(root, "contracts", "host-compatibility.json"), "utf8"));
const source = readFileSync(path.join(root, "host", "Authority", "ExactGameCompatibility.cs"), "utf8");
const artifactSource = readFileSync(path.join(root, "host", "Authority", "ExactArtifactCompatibility.cs"), "utf8");
const failures = [];

if (contract.schema_version !== 1) failures.push("unsupported compatibility schema");
if (!source.includes(`"${contract.canary_environment_variable}"`)) {
  failures.push("C# canary environment variable differs from the compatibility contract");
}
if (!artifactSource.includes(`"${contract.artifact_canary_environment_variable}"`)) {
  failures.push("C# artifact canary environment variable differs from the compatibility contract");
}
for (const artifact of contract.sealed_artifacts ?? []) {
  for (const value of [artifact.source_revision, artifact.artifact_sha256, artifact.artifact_mvid]) {
    if (!artifactSource.includes(value)) failures.push(`${artifact.release}: C# source is missing ${value}`);
  }
}
for (const runtime of contract.runtimes ?? []) {
  for (const value of [
    runtime.id,
    runtime.platform,
    runtime.architecture,
    runtime.game_version,
    runtime.game_commit,
    String(runtime.runtime_main_assembly_hash),
    runtime.main_assembly_sha256,
    runtime.main_assembly_mvid
  ]) {
    if (!source.includes(value)) failures.push(`${runtime.id}: C# source is missing ${value}`);
  }
}
if (failures.length > 0) {
  console.error(["Host compatibility checks failed:", ...failures.map(item => `- ${item}`)].join("\n"));
  process.exitCode = 1;
} else {
  console.log(`host compatibility checks passed (${contract.runtimes.length} exact tuples)`);
}
