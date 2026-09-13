// Deployment owns preserving operator locations; native Annotator owns their meaning.
import fs from "node:fs";
import path from "node:path";

function validatePaths(value) {
  for (const key of ["recording_root", "runtime_status_path"]) {
    if (typeof value[key] !== "string" || !path.isAbsolute(value[key]) || value[key].includes("\0")) {
      throw new Error(`Annotator ${key} must be an explicit absolute path.`);
    }
  }
  return value;
}

export function readAnnotatorConfiguration(file, defaults) {
  let existing = {};
  if (fs.existsSync(file)) {
    existing = JSON.parse(fs.readFileSync(file, "utf8").replace(/^\uFEFF/u, ""));
    if (!existing || typeof existing !== "object" || Array.isArray(existing)) {
      throw new Error("Existing Annotator configuration must be an object; repair it before deployment.");
    }
  }
  return validatePaths({ ...defaults, ...existing });
}

export function effectiveAnnotatorConfiguration(configuration, environment) {
  return validatePaths({
    ...configuration,
    recording_root: environment.STS2_HUMAN_ANNOTATOR_RECORDING_ROOT ?? configuration.recording_root,
    runtime_status_path: environment.STS2_HUMAN_ANNOTATOR_STATUS_PATH ?? configuration.runtime_status_path
  });
}
