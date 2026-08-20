import os from "node:os";
import { execFileSync } from "node:child_process";

function sysctlInteger(name) {
  try {
    const value = Number(execFileSync("sysctl", ["-n", name], { encoding: "utf8" }).trim());
    return Number.isSafeInteger(value) && value > 0 ? value : null;
  } catch {
    return null;
  }
}

export function readSystemIdentity() {
  const cpus = os.cpus();
  const darwin = process.platform === "darwin";
  const physicalCoreCount = darwin ? sysctlInteger("hw.physicalcpu") : null;
  const performanceCoreCount = darwin ? sysctlInteger("hw.perflevel0.physicalcpu") : null;
  const efficiencyCoreCount = darwin ? sysctlInteger("hw.perflevel1.physicalcpu") : null;
  const unavailableFields = ["storage_kind"];
  if (physicalCoreCount == null) unavailableFields.push("physical_core_count");
  if (performanceCoreCount == null) unavailableFields.push("performance_core_count");
  if (efficiencyCoreCount == null) unavailableFields.push("efficiency_core_count");
  return {
    platform: process.platform,
    architecture: process.arch,
    os_release: os.release(),
    node_version: process.version,
    cpu_model: cpus[0]?.model?.trim() ?? null,
    logical_cpu_count: cpus.length,
    physical_core_count: physicalCoreCount,
    performance_core_count: performanceCoreCount,
    efficiency_core_count: efficiencyCoreCount,
    affinity_control: darwin ? "not_exposed_by_portable_node_runtime" : "not_measured",
    total_memory_bytes: os.totalmem(),
    storage_kind: null,
    unavailable_fields: unavailableFields
  };
}
