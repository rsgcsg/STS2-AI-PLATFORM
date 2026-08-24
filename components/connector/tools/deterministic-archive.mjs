import fs from "node:fs";
import path from "node:path";
import { gzipSync } from "node:zlib";

const BLOCK_SIZE = 512;

function archiveName(relative) {
  const normalized = relative.split(path.sep).join("/");
  if (Buffer.byteLength(normalized) <= 100) return { name: normalized, prefix: "" };
  const split = normalized.lastIndexOf("/");
  const prefix = normalized.slice(0, split);
  const name = normalized.slice(split + 1);
  if (split < 0 || Buffer.byteLength(name) > 100 || Buffer.byteLength(prefix) > 155) {
    throw new Error(`Release path is too long for deterministic ustar: ${relative}`);
  }
  return { name, prefix };
}

function writeString(header, offset, length, value) {
  const encoded = Buffer.from(value, "utf8");
  if (encoded.length > length) throw new Error(`Tar field exceeds ${length} bytes: ${value}`);
  encoded.copy(header, offset);
}

function writeOctal(header, offset, length, value) {
  const encoded = value.toString(8).padStart(length - 1, "0");
  if (encoded.length >= length) throw new Error(`Tar numeric field exceeds ${length} bytes: ${value}`);
  writeString(header, offset, length, `${encoded}\0`);
}

function headerFor(relative, size) {
  const { name, prefix } = archiveName(relative);
  const header = Buffer.alloc(BLOCK_SIZE);
  writeString(header, 0, 100, name);
  writeOctal(header, 100, 8, 0o644);
  writeOctal(header, 108, 8, 0);
  writeOctal(header, 116, 8, 0);
  writeOctal(header, 124, 12, size);
  writeOctal(header, 136, 12, 0);
  header.fill(0x20, 148, 156);
  writeString(header, 156, 1, "0");
  writeString(header, 257, 6, "ustar\0");
  writeString(header, 263, 2, "00");
  writeString(header, 265, 32, "root");
  writeString(header, 297, 32, "root");
  writeString(header, 345, 155, prefix);
  const checksum = header.reduce((sum, byte) => sum + byte, 0);
  writeString(header, 148, 8, `${checksum.toString(8).padStart(6, "0")}\0 `);
  return header;
}

function filesUnder(root) {
  const files = [];
  function visit(directory) {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      const file = path.join(directory, entry.name);
      if (entry.isDirectory()) visit(file);
      else if (entry.isFile()) files.push(path.relative(root, file));
      else throw new Error(`Release staging contains a non-file entry: ${file}`);
    }
  }
  visit(root);
  return files.sort((left, right) => left.localeCompare(right, "en"));
}

export function createDeterministicTarGzip(sourceRoot, destination) {
  const blocks = [];
  for (const relative of filesUnder(sourceRoot)) {
    const content = fs.readFileSync(path.join(sourceRoot, relative));
    blocks.push(headerFor(relative, content.length), content);
    const remainder = content.length % BLOCK_SIZE;
    if (remainder !== 0) blocks.push(Buffer.alloc(BLOCK_SIZE - remainder));
  }
  blocks.push(Buffer.alloc(BLOCK_SIZE * 2));
  const compressed = gzipSync(Buffer.concat(blocks), { level: 9, mtime: 0 });
  compressed[9] = 255;
  fs.writeFileSync(destination, compressed);
}
