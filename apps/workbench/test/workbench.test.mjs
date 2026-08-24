import assert from "node:assert/strict";
import { mkdtemp, mkdir, readFile, writeFile } from "node:fs/promises";
import { after, before, test } from "node:test";
import os from "node:os";
import path from "node:path";

import { parseArgs } from "../bin/workbench.mjs";
import { renderHtml } from "../src/render-html.mjs";
import { createWorkbenchServer } from "../src/server.mjs";
import { createWorkbenchService } from "../src/workbench-service.mjs";

let tempRoot;

before(async () => {
  tempRoot = await mkdtemp(path.join(os.tmpdir(), "sts2-workbench-"));
});

after(async () => {
  // The service is read-only; test cleanup is limited to its private fixture root.
  await import("node:fs/promises").then(({ rm }) => rm(tempRoot, { recursive: true, force: true }));
});

function roots() {
  return Object.fromEntries([
    "environment",
    "annotator",
    "evidence",
    "transfer",
    "diagnostics"
  ].map((name) => [name, path.join(tempRoot, name)]));
}

test("missing configured roots are explicit absent and never available", async () => {
  const status = await createWorkbenchService(roots()).readStatus();
  assert.equal(status.overall.state, "unknown");
  for (const service of status.services) {
    assert.equal(service.state, "absent");
    assert.equal(service.root.state, "absent");
    assert.equal(service.status.reason, "directory_missing");
  }
});

test("valid, missing, and malformed status files remain distinguishable", async () => {
  const configured = roots();
  configured.annotator = path.join(tempRoot, "api-missing-annotator");
  await mkdir(configured.environment, { recursive: true });
  await mkdir(configured.annotator, { recursive: true });
  await mkdir(configured.evidence, { recursive: true });
  await mkdir(configured.transfer, { recursive: true });
  await mkdir(configured.diagnostics, { recursive: true });
  await writeFile(
    path.join(configured.environment, "runtime-status.json"),
    JSON.stringify({ observed: "fixture-state", revision: "fixture-revision" })
  );
  await writeFile(path.join(configured.annotator, "runtime-status.json"), "{not-json\n");
  await writeFile(path.join(configured.transfer, "transfer-status.json"), "null\n");

  const status = await createWorkbenchService(configured).readStatus();
  const byName = Object.fromEntries(status.services.map((service) => [service.name, service]));
  assert.equal(byName.environment.state, "available");
  assert.equal(byName.environment.status.state, "known");
  assert.equal(byName.environment.status.value.observed, "fixture-state");
  assert.equal(byName.annotator.state, "unknown");
  assert.equal(byName.annotator.status.reason, "status_invalid_json");
  assert.equal(byName.evidence.state, "unknown");
  assert.equal(byName.evidence.status.state, "absent");
  assert.equal(byName.transfer.state, "unknown");
  assert.equal(byName.transfer.status.reason, "status_json_not_object");
  assert.equal(byName.diagnostics.state, "unknown");
  assert.equal(status.overall.state, "unknown");
});

test("unconfigured roots are unknown and CLI configuration is explicit", () => {
  const statusPromise = createWorkbenchService().readStatus();
  assert.deepEqual(parseArgs([
    "--root", "environment=/tmp/environment",
    "--annotator-root", "/tmp/annotator",
    "--port", "0"
  ]), {
    help: false,
    host: "127.0.0.1",
    port: 0,
    roots: {
      environment: "/tmp/environment",
      annotator: "/tmp/annotator"
    }
  });
  return statusPromise.then((status) => {
    assert.equal(status.overall.state, "unknown");
    assert.ok(status.services.every((service) => service.state === "unknown"));
  });
});

test("official Evidence store status and transfer receipt are recognized", async () => {
  const configured = roots();
  configured.evidence = path.join(tempRoot, "official-evidence");
  configured.transfer = path.join(tempRoot, "official-transfer");
  await mkdir(configured.evidence, { recursive: true });
  await mkdir(configured.transfer, { recursive: true });
  await writeFile(
    path.join(configured.evidence, "store-status.json"),
    '{"schema":"sts2.evidence/store-status-1","last_receipt":{"status":"promoted"}}\n'
  );
  await writeFile(
    path.join(configured.transfer, "transfer-receipt.json"),
    '{"status":"promoted","content_id":"fixture"}\n'
  );

  const status = await createWorkbenchService(configured).readStatus();
  const byName = Object.fromEntries(status.services.map((service) => [service.name, service]));
  assert.equal(byName.evidence.state, "available");
  assert.equal(byName.evidence.status.value.last_receipt.status, "promoted");
  assert.equal(byName.transfer.state, "available");
  assert.equal(byName.transfer.status.value.content_id, "fixture");
});

test("JSON API and HTML render the same read-only DTO", async () => {
  const configured = roots();
  await mkdir(configured.environment, { recursive: true });
  await writeFile(path.join(configured.environment, "runtime-status.json"), "{}\n");
  const service = createWorkbenchService(configured);
  const server = createWorkbenchServer(service);
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  const address = server.address();
  const base = `http://127.0.0.1:${address.port}`;
  try {
    const apiResponse = await fetch(`${base}/api/status`);
    const apiStatus = await apiResponse.json();
    assert.equal(apiResponse.status, 200);
    assert.equal(apiStatus.read_only, true);
    assert.equal(apiStatus.services.find((item) => item.name === "environment").status.state, "known");
    assert.equal(apiStatus.services.find((item) => item.name === "annotator").state, "absent");

    const htmlResponse = await fetch(`${base}/`);
    const html = await htmlResponse.text();
    assert.equal(htmlResponse.status, 200);
    assert.match(html, /read-only filesystem view/u);
    assert.match(html, /environment/u);
    assert.match(html, /absent/u);
    assert.match(html, /Raw service DTO/u);
    assert.match(renderHtml(apiStatus), /overall: unknown/u);

    const methodResponse = await fetch(`${base}/api/status`, { method: "POST" });
    assert.equal(methodResponse.status, 405);
  } finally {
    await new Promise((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));
  }
});

test("service reads do not modify a status file", async () => {
  const configured = roots();
  await mkdir(configured.environment, { recursive: true });
  const statusPath = path.join(configured.environment, "runtime-status.json");
  await writeFile(statusPath, '{"observed":"unchanged"}\n');
  const before = await readFile(statusPath, "utf8");
  await createWorkbenchService(configured).readStatus();
  assert.equal(await readFile(statusPath, "utf8"), before);
});
