import fs from "node:fs/promises";
import path from "node:path";

import { PolicyRuntimeClient } from "./policy-runtime-client.mjs";

export const SERVICE_NAMES = Object.freeze([
  "environment",
  "policy",
  "annotator",
  "human_data",
  "evidence",
  "transfer",
  "diagnostics"
]);

const STATUS_CANDIDATES = Object.freeze({
  environment: ["runtime-status.json", "environment-status.json", "status.json"],
  policy: ["runtime-status.json", "policy-runtime-status.json", "status.json"],
  annotator: ["runtime-status.json", "recording-status.json", "status.json"],
  human_data: ["human-data-status.json", "status.json"],
  evidence: ["store-status.json", "status.json"],
  transfer: ["transfer-status.json", "transfer-receipt.json", "status.json"],
  diagnostics: ["diagnostics.json", "status.json"]
});

const MAX_STATUS_BYTES = 1024 * 1024;

function errorCode(error) {
  return typeof error?.code === "string" ? error.code : "unknown_error";
}

function rootPath(value) {
  if (typeof value !== "string" || value.trim() === "") return null;
  return path.resolve(value);
}

function unknownRoot(reason, configuredPath = null) {
  return {
    state: "unknown",
    path: configuredPath,
    reason
  };
}

async function inspectRoot(root) {
  if (!root) return unknownRoot("not_configured");
  try {
    const info = await fs.stat(root);
    if (!info.isDirectory()) {
      return { state: "unknown", path: root, reason: "not_a_directory" };
    }
    return { state: "available", path: root };
  } catch (error) {
    if (errorCode(error) === "ENOENT") {
      return { state: "absent", path: root, reason: "directory_missing" };
    }
    return { state: "unknown", path: root, reason: `root_${errorCode(error)}` };
  }
}

async function inspectEntries(root) {
  try {
    const entries = await fs.readdir(root, { withFileTypes: true });
    return {
      state: "available",
      files: entries.filter((entry) => entry.isFile()).length,
      directories: entries.filter((entry) => entry.isDirectory()).length,
      names: entries.map((entry) => entry.name).sort()
    };
  } catch (error) {
    return { state: "unknown", reason: `directory_${errorCode(error)}` };
  }
}

async function findStatusFile(root, candidates) {
  for (const candidate of candidates) {
    const candidatePath = path.join(root, candidate);
    try {
      const info = await fs.stat(candidatePath);
      if (!info.isFile()) {
        return { state: "unknown", path: candidatePath, reason: "status_path_not_file" };
      }
      if (info.size > MAX_STATUS_BYTES) {
        return { state: "unknown", path: candidatePath, reason: "status_file_too_large" };
      }
      return { state: "present", path: candidatePath };
    } catch (error) {
      if (errorCode(error) === "ENOENT") continue;
      return { state: "unknown", path: candidatePath, reason: `status_${errorCode(error)}` };
    }
  }
  return { state: "absent", reason: "no_known_status_file" };
}

async function readStatusFile(statusFile) {
  if (statusFile.state !== "present") return statusFile;
  try {
    const raw = await fs.readFile(statusFile.path, "utf8");
    const value = JSON.parse(raw);
    if (value === null || typeof value !== "object" || Array.isArray(value)) {
      return { ...statusFile, state: "unknown", reason: "status_json_not_object" };
    }
    return { ...statusFile, state: "known", value };
  } catch (error) {
    if (error instanceof SyntaxError) {
      return { ...statusFile, state: "unknown", reason: "status_invalid_json" };
    }
    return { ...statusFile, state: "unknown", reason: `status_read_${errorCode(error)}` };
  }
}

function serviceState(root, status) {
  if (root.state === "absent") return "absent";
  if (root.state !== "available") return "unknown";
  return status.state === "known" ? "available" : "unknown";
}

function decorateFilesystemService(service) {
  const filesystemAvailable = service.state === "available";
  return {
    ...service,
    source: filesystemAvailable ? "filesystem" : "none",
    freshness: filesystemAvailable ? "filesystem" : "unknown",
    partial: !filesystemAvailable,
    unavailable: !filesystemAvailable
  };
}

async function inspectService(name, configuredRoot) {
  const root = rootPath(configuredRoot);
  const rootStatus = await inspectRoot(root);
  if (rootStatus.state !== "available") {
    return {
      name,
      state: rootStatus.state,
      root: rootStatus,
      status: { state: rootStatus.state, reason: rootStatus.reason },
      entries: { state: rootStatus.state, reason: rootStatus.reason }
    };
  }

  const entries = await inspectEntries(root);
  const statusFile = await findStatusFile(root, STATUS_CANDIDATES[name]);
  const status = await readStatusFile(statusFile);
  return {
    name,
    state: serviceState(rootStatus, status),
    root: rootStatus,
    status,
    entries
  };
}

async function inspectPolicyService(configuredRoot, policyRuntime) {
  const filesystem = decorateFilesystemService(await inspectService("policy", configuredRoot));
  if (!policyRuntime?.configured) {
    return {
      ...filesystem,
      state: filesystem.status.state === "known" ? "partial" : filesystem.state,
      source: filesystem.status.state === "known" ? "filesystem" : "none",
      freshness: filesystem.status.state === "known" ? "filesystem" : "unknown",
      partial: true,
      unavailable: true,
      upstream: { state: "unavailable", reason: "not_configured" }
    };
  }

  try {
    const value = await policyRuntime.readStatus();
    return {
      ...filesystem,
      state: "available",
      source: "policy_runtime",
      freshness: "live",
      partial: false,
      unavailable: false,
      status: { state: "known", source: "policy_runtime", value },
      upstream: { state: "available", source: "policy_runtime" }
    };
  } catch (error) {
    const fallbackKnown = filesystem.status.state === "known";
    return {
      ...filesystem,
      state: fallbackKnown ? "partial" : "unavailable",
      source: fallbackKnown ? "filesystem_fallback" : "policy_runtime",
      freshness: fallbackKnown ? "filesystem" : "unknown",
      partial: true,
      unavailable: true,
      upstream: {
        state: "unavailable",
        reason: error?.code ?? "policy_runtime_unavailable"
      }
    };
  }
}

function overallState(services) {
  return services.every((service) => service.state === "available") ? "available" : "unknown";
}

export function createWorkbenchService(roots = {}, options = {}) {
  const configuredRoots = Object.fromEntries(
    SERVICE_NAMES.map((name) => [name, roots[name] ?? null])
  );
  const policyRuntime = options.policyRuntimeClient ?? new PolicyRuntimeClient(
    options.policyRuntimeUrl ?? null,
    { timeoutMs: options.policyRuntimeTimeoutMs }
  );

  return Object.freeze({
    async readStatus() {
      const services = await Promise.all(
        SERVICE_NAMES.map((name) => name === "policy"
          ? inspectPolicyService(configuredRoots[name], policyRuntime)
          : inspectService(name, configuredRoots[name]).then(decorateFilesystemService))
      );
      const overallStateValue = overallState(services);
      return {
        schema: "sts2.workbench/status-1",
        generated_at: new Date().toISOString(),
        read_only: false,
        overall: {
          state: overallStateValue,
          reason: overallStateValue === "available"
            ? "all_configured_status_files_read"
            : "one_or_more_domains_partial_or_unavailable"
        },
        services,
        roots: Object.fromEntries(services.map((service) => [service.name, service.root]))
      };
    },
    async setPolicyMode(mode) {
      const changed = await policyRuntime.setMode(mode);
      if (mode !== "one_step") return changed;
      const ticked = await policyRuntime.tick();
      return { ...changed, status: ticked.status, tick: ticked.result };
    }
  });
}

export function defaultStatusCandidates() {
  return Object.fromEntries(
    SERVICE_NAMES.map((name) => [name, [...STATUS_CANDIDATES[name]]])
  );
}
