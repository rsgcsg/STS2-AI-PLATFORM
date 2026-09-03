import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";

const root = path.resolve(import.meta.dirname, "..");

test("repository pins text materialization to LF for stable source digests", () => {
  const attributes = fs.readFileSync(path.join(root, ".gitattributes"), "utf8");
  assert.match(attributes, /^\* text=auto eol=lf\s*$/mu);
});
