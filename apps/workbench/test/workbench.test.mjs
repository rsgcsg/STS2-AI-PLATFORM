import assert from "node:assert/strict";
import { mkdtemp, mkdir, readFile, writeFile } from "node:fs/promises";
import { after, before, test } from "node:test";
import { createServer } from "node:http";
import os from "node:os";
import path from "node:path";

import { parseArgs } from "../bin/workbench.mjs";
import { renderHtml } from "../src/render-html.mjs";
import { createWorkbenchService } from "../src/workbench-service.mjs";
import { createWorkbenchServer } from "../src/server.mjs";
import { PolicyRuntimeClient } from "../src/policy-runtime-client.mjs";

let tempRoot;

const POLICY_STATUS = {
  schema: "sts2.policy-runtime/status-1",
  runtime: { version: "0.1.0-rc.1", code_sha256: "d".repeat(64) },
  policy: {
    manifest_id: "fixture-manifest",
    policy_id: "fixture-policy",
    policy_version: "1",
    provider: "fixture",
    architecture: "fixture",
    artifact_sha256: "a".repeat(64)
  },
  run_id: "run-fixture",
  lifecycle: "running",
  mode: "human",
  controller: "released",
  tainted: false,
  taint_reason: null,
  refreshing: false,
  last_snapshot_id: null,
  last_snapshot: null,
  last_decision: null,
  last_receipt: null,
  reads: [],
  invalidations: [],
  errors: [],
  environment: null
};

const policyEnvelope = (status = POLICY_STATUS) => ({
  schema: "sts2.policy-runtime/http-1",
  status
});

async function startPolicyRuntime(handler) {
  const server = createServer(handler);
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  const address = server.address();
  return {
    baseUrl: `http://127.0.0.1:${address.port}`,
    close: () => new Promise((resolve, reject) => server.close((error) => error ? reject(error) : resolve()))
  };
}

before(async () => {
  tempRoot = await mkdtemp(path.join(os.tmpdir(), "sts2-workbench-"));
});

after(async () => {
  // Test cleanup is limited to its private fixture root.
  await import("node:fs/promises").then(({ rm }) => rm(tempRoot, { recursive: true, force: true }));
});

function roots() {
  return Object.fromEntries([
    "environment",
    "policy",
    "annotator",
    "human_data",
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
    assert.equal(service.source, "none");
    assert.equal(service.freshness, "unknown");
    assert.equal(service.partial, true);
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
  assert.equal(byName.environment.source, "filesystem");
  assert.equal(byName.environment.freshness, "filesystem");
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
    },
    policyRuntimeUrl: null
  });
  return statusPromise.then((status) => {
    assert.equal(status.overall.state, "unknown");
    assert.ok(status.services.every((service) => service.state === "unknown"));
  });
});

test("named Policy Runtime and Human Data roots are explicit CLI configuration", () => {
  assert.deepEqual(parseArgs([
    "--policy-runtime-url", "http://127.0.0.1:15527",
    "--policy-root", "/tmp/policy",
    "--human-data-root", "/tmp/human-data"
  ]), {
    help: false,
    host: "127.0.0.1",
    port: 8787,
    roots: {
      policy: "/tmp/policy",
      human_data: "/tmp/human-data"
    },
    policyRuntimeUrl: "http://127.0.0.1:15527"
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

test("JSON API and HTML render the same typed status DTO", async () => {
  const configured = roots();
  await mkdir(configured.environment, { recursive: true });
  await writeFile(path.join(configured.environment, "runtime-status.json"), "{}\n");
  const service = createWorkbenchService(configured);
  const server = createWorkbenchServer(service, { bindHost: "127.0.0.1" });
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  const address = server.address();
  const base = `http://127.0.0.1:${address.port}`;
  try {
    const apiResponse = await fetch(`${base}/api/status`);
    const apiStatus = await apiResponse.json();
    assert.equal(apiResponse.status, 200);
    assert.equal(apiStatus.read_only, false);
    assert.equal(apiStatus.services.find((item) => item.name === "environment").status.state, "known");
    assert.equal(apiStatus.services.find((item) => item.name === "annotator").state, "absent");

    const htmlResponse = await fetch(`${base}/`);
    const html = await htmlResponse.text();
    assert.equal(htmlResponse.status, 200);
    assert.match(html, /typed status view with explicit filesystem fallback/u);
    assert.match(html, /environment/u);
    assert.match(html, /absent/u);
    assert.match(html, /Raw service DTO/u);
    assert.match(html, /Policy controls/u);
    assert.match(html, /disabled: live runtime unavailable/u);
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

test("Policy Runtime status is typed and marked live", async () => {
  const runtime = await startPolicyRuntime((request, response) => {
    assert.equal(request.method, "GET");
    assert.equal(request.url, "/status");
    response.setHeader("content-type", "application/json");
    response.end(JSON.stringify(policyEnvelope()));
  });
  try {
    const status = await createWorkbenchService(roots(), {
      policyRuntimeUrl: runtime.baseUrl
    }).readStatus();
    const policy = status.services.find((service) => service.name === "policy");
    assert.equal(policy.state, "available");
    assert.equal(policy.source, "policy_runtime");
    assert.equal(policy.freshness, "live");
    assert.equal(policy.partial, false);
    assert.equal(policy.status.value.schema, "sts2.policy-runtime/status-1");
  } finally {
    await runtime.close();
  }
});

test("unavailable Policy Runtime falls back only as partial filesystem state", async () => {
  const configured = roots();
  await mkdir(configured.policy, { recursive: true });
  await writeFile(path.join(configured.policy, "runtime-status.json"), `${JSON.stringify(POLICY_STATUS)}\n`);
  const unused = await startPolicyRuntime((_request, response) => response.destroy());
  await unused.close();
  const status = await createWorkbenchService(configured, {
    policyRuntimeUrl: unused.baseUrl,
    policyRuntimeTimeoutMs: 100
  }).readStatus();
  const policy = status.services.find((service) => service.name === "policy");
  assert.equal(policy.state, "partial");
  assert.equal(policy.source, "filesystem_fallback");
  assert.equal(policy.freshness, "filesystem");
  assert.equal(policy.partial, true);
  assert.equal(policy.unavailable, true);
  assert.equal(policy.upstream.state, "unavailable");
});

test("invalid Policy Runtime schema is fail closed", async () => {
  const runtime = await startPolicyRuntime((_request, response) => {
    response.setHeader("content-type", "application/json");
    response.end(JSON.stringify(policyEnvelope({ ...POLICY_STATUS, unexpected: true })));
  });
  try {
    await assert.rejects(
      () => new PolicyRuntimeClient(runtime.baseUrl).readStatus(),
      (error) => error.code === "policy_runtime_invalid_schema"
    );
    const configured = roots();
    configured.policy = path.join(tempRoot, "invalid-schema-policy");
    const policy = (await createWorkbenchService(configured, {
      policyRuntimeUrl: runtime.baseUrl
    }).readStatus()).services.find((service) => service.name === "policy");
    assert.equal(policy.state, "unavailable");
    assert.equal(policy.source, "policy_runtime");
    assert.equal(policy.partial, true);
    assert.equal(policy.upstream.reason, "policy_runtime_invalid_schema");
  } finally {
    await runtime.close();
  }
});

test("mode command allowlist rejects invalid values and forwards valid values", async () => {
  let receivedMode;
  let tickCount = 0;
  const runtime = await startPolicyRuntime(async (request, response) => {
    response.setHeader("content-type", "application/json");
    if (request.method === "POST" && request.url === "/mode") {
      const chunks = [];
      for await (const chunk of request) chunks.push(chunk);
      receivedMode = JSON.parse(Buffer.concat(chunks).toString("utf8")).mode;
      response.end(JSON.stringify({
        schema: "sts2.policy-runtime/http-1",
        status: { ...POLICY_STATUS, mode: receivedMode }
      }));
      return;
    }
    if (request.method === "POST" && request.url === "/tick") {
      const chunks = [];
      for await (const chunk of request) chunks.push(chunk);
      assert.deepEqual(JSON.parse(Buffer.concat(chunks).toString("utf8")), { max_ticks: 1 });
      tickCount += 1;
      response.end(JSON.stringify({
        schema: "sts2.policy-runtime/http-1/tick-1",
        results: [{ type: "not_executed" }],
        status: { ...POLICY_STATUS, mode: "human" }
      }));
      return;
    }
    response.end(JSON.stringify(policyEnvelope()));
  });
  const workbench = createWorkbenchServer(createWorkbenchService(roots(), {
    policyRuntimeUrl: runtime.baseUrl
  }), { bindHost: "127.0.0.1" });
  await new Promise((resolve) => workbench.listen(0, "127.0.0.1", resolve));
  const address = workbench.address();
  const base = `http://127.0.0.1:${address.port}`;
  try {
    const invalid = await fetch(`${base}/api/policy/mode`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ mode: "play_card" })
    });
    assert.equal(invalid.status, 400);
    assert.equal(receivedMode, undefined);

    const valid = await fetch(`${base}/api/policy/mode`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ mode: "one_step" })
    });
    assert.equal(valid.status, 200);
    assert.equal(receivedMode, "one_step");
    const result = await valid.json();
    assert.equal(result.mode, "one_step");
    assert.equal(result.tick.type, "not_executed");
    assert.equal(result.status.mode, "human");
    assert.equal(tickCount, 1);
  } finally {
    await new Promise((resolve, reject) => workbench.close((error) => error ? reject(error) : resolve()));
    await runtime.close();
  }
});

test("non-loopback binds expose status read-only and never forward policy mutations", async () => {
  let forwarded = false;
  const runtime = await startPolicyRuntime((request, response) => {
    if (request.method === "POST") forwarded = true;
    response.setHeader("content-type", "application/json");
    response.end(JSON.stringify(policyEnvelope()));
  });
  const workbench = createWorkbenchServer(createWorkbenchService(roots(), {
    policyRuntimeUrl: runtime.baseUrl
  }), { bindHost: "0.0.0.0" });
  await new Promise((resolve) => workbench.listen(0, "127.0.0.1", resolve));
  const address = workbench.address();
  const base = `http://127.0.0.1:${address.port}`;
  try {
    const status = await fetch(`${base}/api/status`);
    assert.equal(status.status, 200);
    assert.equal((await status.json()).read_only, true);

    const response = await fetch(`${base}/api/policy/mode`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ mode: "human" })
    });
    assert.equal(response.status, 403);
    assert.equal((await response.json()).error, "policy_mutation_loopback_only");
    assert.equal(forwarded, false);
  } finally {
    await new Promise((resolve, reject) => workbench.close((error) => error ? reject(error) : resolve()));
    await runtime.close();
  }
});
