import { closeSync, fsyncSync, mkdirSync, openSync, statSync, writeSync } from "node:fs";
import path from "node:path";

export class JsonlRecorder {
  #baseFile;
  #file;
  #fd;
  #flushEvery;
  #maxBytes;
  #recordsSinceFlush = 0;
  #bytes = 0;
  #part = 0;
  #closed = false;

  constructor(file, { flushEvery = 1, maxBytes = 64 * 1024 * 1024 } = {}) {
    if (!Number.isSafeInteger(flushEvery) || flushEvery < 1) {
      throw new Error("flushEvery must be a positive integer.");
    }
    if (!Number.isSafeInteger(maxBytes) || maxBytes < 1) {
      throw new Error("maxBytes must be a positive integer.");
    }
    this.#baseFile = file;
    this.#flushEvery = flushEvery;
    this.#maxBytes = maxBytes;
    mkdirSync(path.dirname(file), { recursive: true });
    this.#open(file);
  }

  get files() {
    return Array.from({ length: this.#part + 1 }, (_, index) => this.#partFile(index));
  }

  append(record) {
    if (this.#closed) throw new Error("Cannot append to a closed JSONL recorder.");
    const line = `${JSON.stringify(record)}\n`;
    const bytes = Buffer.byteLength(line);
    if (this.#bytes > 0 && this.#bytes + bytes > this.#maxBytes) this.#rotate();
    writeSync(this.#fd, line, null, "utf8");
    this.#bytes += bytes;
    this.#recordsSinceFlush += 1;
    if (this.#recordsSinceFlush >= this.#flushEvery) this.flush();
  }

  flush() {
    if (this.#closed) return;
    fsyncSync(this.#fd);
    this.#recordsSinceFlush = 0;
  }

  close() {
    if (this.#closed) return;
    this.flush();
    closeSync(this.#fd);
    this.#closed = true;
  }

  #partFile(index) {
    if (index === 0) return this.#baseFile;
    const extension = path.extname(this.#baseFile);
    const stem = extension ? this.#baseFile.slice(0, -extension.length) : this.#baseFile;
    return `${stem}.part-${String(index + 1).padStart(4, "0")}${extension}`;
  }

  #open(file) {
    this.#file = file;
    this.#fd = openSync(file, "a");
    this.#bytes = statSync(file).size;
  }

  #rotate() {
    this.flush();
    closeSync(this.#fd);
    this.#part += 1;
    this.#open(this.#partFile(this.#part));
  }
}
