import { createHash, randomUUID } from "node:crypto";
import {
  cpSync,
  existsSync,
  lstatSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  renameSync,
  rmSync,
  writeFileSync
} from "node:fs";
import path from "node:path";
import {
  isolatedProfilePaths,
  prepareIsolatedProfile,
  validateProfileId
} from "./profile-isolation.mjs";

function assertContained(root, target) {
  const relative = path.relative(root, target);
  if (!relative || relative.startsWith("..") || path.isAbsolute(relative)) {
    throw new Error(`Refusing template operation outside the template root: ${target}`);
  }
}

export function profileTemplatePaths(localRoot, templateId) {
  validateProfileId(templateId);
  const templatesRoot = path.resolve(localRoot, "profile-templates");
  const templateRoot = path.resolve(templatesRoot, templateId);
  assertContained(templatesRoot, templateRoot);
  return {
    templates_root: templatesRoot,
    template_root: templateRoot,
    home: path.join(templateRoot, "home"),
    manifest: path.join(templateRoot, "template.json")
  };
}

function inventoryFiles(root) {
  const files = [];
  const walk = (directory) => {
    for (const entry of readdirSync(directory).sort()) {
      const absolute = path.join(directory, entry);
      const stat = lstatSync(absolute);
      if (stat.isSymbolicLink()) {
        throw new Error(`Profile templates cannot contain symbolic links: ${absolute}`);
      }
      if (stat.isDirectory()) {
        walk(absolute);
        continue;
      }
      if (!stat.isFile()) throw new Error(`Unsupported profile entry: ${absolute}`);
      const bytes = readFileSync(absolute);
      files.push({
        path: path.relative(root, absolute).replaceAll("\\", "/"),
        size: bytes.length,
        sha256: createHash("sha256").update(bytes).digest("hex")
      });
    }
  };
  walk(root);
  const digest = createHash("sha256");
  for (const file of files) {
    digest.update(`${file.path}\0${file.size}\0${file.sha256}\n`);
  }
  return { files, digest: digest.digest("hex") };
}

export function captureProfileTemplate({
  localRoot,
  profileId,
  templateId,
  gameIdentity
}) {
  const profile = prepareIsolatedProfile(localRoot, profileId);
  const paths = profileTemplatePaths(localRoot, templateId);
  const nativeSettings = path.join(
    profile.expected_user_data_root,
    "default",
    profile.client_id ?? "1",
    "settings.save"
  );
  if (!existsSync(nativeSettings)) {
    throw new Error("The source profile has no native settings.save; bootstrap it before capture.");
  }
  const temporary = `${paths.template_root}.tmp-${randomUUID()}`;
  rmSync(temporary, { recursive: true, force: true });
  mkdirSync(temporary, { recursive: true });
  cpSync(profile.home, path.join(temporary, "home"), { recursive: true });
  const inventory = inventoryFiles(path.join(temporary, "home"));
  const manifest = {
    schema_version: 1,
    template_id: templateId,
    captured_at: new Date().toISOString(),
    source_profile: {
      profile_id: profileId,
      generation_id: profile.generation.generation_id,
      steam: "disabled_before_platform_initialization",
      client_id: "1"
    },
    game_identity: gameIdentity,
    file_count: inventory.files.length,
    payload_sha256: inventory.digest,
    files: inventory.files
  };
  writeFileSync(path.join(temporary, "template.json"), `${JSON.stringify(manifest, null, 2)}\n`);
  rmSync(paths.template_root, { recursive: true, force: true });
  mkdirSync(paths.templates_root, { recursive: true });
  renameSync(temporary, paths.template_root);
  return { status: "captured", ...manifest, template_root: paths.template_root };
}

export function instantiateProfileTemplate({ localRoot, templateId, profileId }) {
  const template = profileTemplatePaths(localRoot, templateId);
  if (!existsSync(template.manifest) || !existsSync(template.home)) {
    throw new Error(`Profile template is incomplete: ${template.template_root}`);
  }
  const manifest = JSON.parse(readFileSync(template.manifest, "utf8"));
  if (manifest.schema_version !== 1 || manifest.template_id !== templateId) {
    throw new Error("Profile template manifest identity is invalid.");
  }
  const inventory = inventoryFiles(template.home);
  if (inventory.digest !== manifest.payload_sha256
      || inventory.files.length !== manifest.file_count) {
    throw new Error("Profile template payload does not match its recorded digest.");
  }
  const target = isolatedProfilePaths(localRoot, profileId);
  rmSync(target.profile_root, { recursive: true, force: true });
  const profile = prepareIsolatedProfile(localRoot, profileId);
  cpSync(template.home, profile.home, { recursive: true, force: true });
  return {
    status: "instantiated",
    template_id: templateId,
    template_payload_sha256: manifest.payload_sha256,
    profile_id: profileId,
    generation_id: profile.generation.generation_id,
    profile_root: profile.profile_root,
    expected_user_data_root: profile.expected_user_data_root,
    game_identity: manifest.game_identity
  };
}
