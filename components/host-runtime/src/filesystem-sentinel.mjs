import { createHash } from "node:crypto";
import {
  existsSync,
  lstatSync,
  readFileSync,
  readdirSync,
  readlinkSync
} from "node:fs";
import os from "node:os";
import path from "node:path";

export function sharedGameUserDataRoot({
  platform = process.platform,
  environment = process.env,
  home = os.homedir()
} = {}) {
  const platformPath = platform === "win32" ? path.win32 : path.posix;
  if (platform === "win32") {
    if (!environment.APPDATA) throw new Error("APPDATA is required to locate the shared STS2 profile.");
    return platformPath.join(environment.APPDATA, "SlayTheSpire2");
  }
  if (platform === "darwin") {
    return platformPath.join(home, "Library", "Application Support", "SlayTheSpire2");
  }
  return platformPath.join(
    environment.XDG_DATA_HOME ?? platformPath.join(home, ".local", "share"),
    "SlayTheSpire2"
  );
}

function updateEntry(hash, kind, relativePath, stat, payloadHash = null) {
  hash.update(JSON.stringify({
    kind,
    path: relativePath.replaceAll(path.sep, "/"),
    mode: Number(stat.mode),
    size: Number(stat.size),
    mtime_ns: stat.mtimeNs.toString(),
    payload_sha256: payloadHash
  }));
  hash.update("\n");
}

export function snapshotFilesystemTree(root) {
  const resolvedRoot = path.resolve(root);
  if (!existsSync(resolvedRoot)) {
    return {
      root: resolvedRoot,
      present: false,
      file_count: 0,
      directory_count: 0,
      symlink_count: 0,
      total_file_bytes: 0,
      tree_sha256: null
    };
  }

  const treeHash = createHash("sha256");
  let fileCount = 0;
  let directoryCount = 0;
  let symlinkCount = 0;
  let totalFileBytes = 0;

  function visit(target, relativePath) {
    const stat = lstatSync(target, { bigint: true });
    if (stat.isSymbolicLink()) {
      const targetHash = createHash("sha256").update(readlinkSync(target)).digest("hex");
      updateEntry(treeHash, "symlink", relativePath, stat, targetHash);
      symlinkCount += 1;
      return;
    }
    if (stat.isDirectory()) {
      updateEntry(treeHash, "directory", relativePath, stat);
      directoryCount += 1;
      for (const entry of readdirSync(target).sort((left, right) => left.localeCompare(right))) {
        visit(path.join(target, entry), relativePath ? path.join(relativePath, entry) : entry);
      }
      return;
    }
    if (!stat.isFile()) {
      throw new Error(`Unsupported filesystem entry in profile sentinel: ${target}`);
    }
    const contentHash = createHash("sha256").update(readFileSync(target)).digest("hex");
    updateEntry(treeHash, "file", relativePath, stat, contentHash);
    fileCount += 1;
    totalFileBytes += Number(stat.size);
  }

  visit(resolvedRoot, "");
  return {
    root: resolvedRoot,
    present: true,
    file_count: fileCount,
    directory_count: directoryCount,
    symlink_count: symlinkCount,
    total_file_bytes: totalFileBytes,
    tree_sha256: treeHash.digest("hex")
  };
}

export function compareFilesystemSnapshots(before, after) {
  if (before.root !== after.root) throw new Error("Filesystem sentinel roots do not match.");
  const unchanged = before.present === after.present
    && before.file_count === after.file_count
    && before.directory_count === after.directory_count
    && before.symlink_count === after.symlink_count
    && before.total_file_bytes === after.total_file_bytes
    && before.tree_sha256 === after.tree_sha256;
  return {
    root: before.root,
    unchanged,
    before,
    after
  };
}
