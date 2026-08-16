import os from "node:os";

export function readSystemIdentity() {
  const cpus = os.cpus();
  return {
    platform: process.platform,
    architecture: process.arch,
    os_release: os.release(),
    node_version: process.version,
    cpu_model: cpus[0]?.model?.trim() ?? null,
    logical_cpu_count: cpus.length,
    physical_core_count: null,
    total_memory_bytes: os.totalmem(),
    storage_kind: null,
    unavailable_fields: ["physical_core_count", "storage_kind"]
  };
}
