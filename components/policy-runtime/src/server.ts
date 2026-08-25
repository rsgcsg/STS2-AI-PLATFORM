import { createServer, type IncomingMessage, type Server, type ServerResponse } from "node:http";
import type { AddressInfo } from "node:net";
import type { PolicyRuntime } from "./runtime.js";
import type { TickResult } from "./contracts.js";

const HTTP_SCHEMA = "sts2.policy-runtime/http-1" as const;

export interface PolicyRuntimeHttpOptions {
  host?: "127.0.0.1" | "localhost" | "::1";
  port?: number;
  maxBodyBytes?: number;
  maxAutoTicks?: number;
  autoDrive?: boolean;
  deferAutoDrive?: boolean;
  autoIdleMs?: number;
}

export interface RunningPolicyRuntimeHttpServer {
  readonly server: Server;
  readonly address: string;
  startDriving(): void;
  close(): Promise<void>;
}

export async function startPolicyRuntimeHttpServer(runtime: PolicyRuntime, options: PolicyRuntimeHttpOptions = {}): Promise<RunningPolicyRuntimeHttpServer> {
  const host = options.host ?? "127.0.0.1";
  if (!["127.0.0.1", "localhost", "::1"].includes(host)) throw new Error("Policy Runtime HTTP service is loopback-only");
  const maxBodyBytes = options.maxBodyBytes ?? 8 * 1024;
  const maxAutoTicks = options.maxAutoTicks ?? 16;
  const autoIdleMs = options.autoIdleMs ?? 25;
  if (!Number.isSafeInteger(maxBodyBytes) || maxBodyBytes < 1 || !Number.isSafeInteger(maxAutoTicks) || maxAutoTicks < 1) throw new Error("HTTP bounds must be positive integers");
  if (!Number.isSafeInteger(autoIdleMs) || autoIdleMs < 0) throw new Error("autoIdleMs must be a non-negative integer");
  let closing = false;
  let autoWorker: Promise<void> | null = null;
  const ensureAutoWorker = (): void => {
    if (!options.autoDrive || autoWorker || closing || !isDrivenMode(runtime.status().mode)) return;
    autoWorker = (async () => {
      try {
        while (!closing && isDrivenMode(runtime.status().mode) && !runtime.status().tainted) {
          const result = await runtime.tick();
          if (result.type === "unknown" || !isDrivenMode(runtime.status().mode)) return;
          if (autoIdleMs > 0) await new Promise((resolve) => setTimeout(resolve, autoIdleMs));
          else await new Promise((resolve) => setImmediate(resolve));
        }
      } catch {
        try { await runtime.setMode("human"); } catch { /* Runtime already failed closed. */ }
      }
    })().finally(() => { autoWorker = null; if (!closing) ensureAutoWorker(); });
  };
  const server = createServer((request, response) => { void dispatch(runtime, request, response, maxBodyBytes, maxAutoTicks, ensureAutoWorker); });
  await new Promise<void>((resolve, reject) => { server.once("error", reject); server.listen(options.port ?? 0, host, () => { server.removeListener("error", reject); resolve(); }); });
  const address = server.address();
  if (!address || typeof address === "string") throw new Error("Policy Runtime HTTP service did not expose a socket address");
  if (!options.deferAutoDrive) ensureAutoWorker();
  return { server, address: `http://${host}:${(address as AddressInfo).port}`, startDriving: ensureAutoWorker, close: async () => {
    closing = true;
    await new Promise<void>((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));
    await autoWorker;
  } };
}

async function dispatch(runtime: PolicyRuntime, request: IncomingMessage, response: ServerResponse, maxBodyBytes: number, maxAutoTicks: number, ensureAutoWorker: () => void): Promise<void> {
  try {
    if (request.method === "GET" && request.url === "/status") { json(response, 200, { schema: HTTP_SCHEMA, status: runtime.status() }); return; }
    if (request.method !== "POST" || !["/mode", "/tick", "/stop"].includes(request.url ?? "")) { json(response, 404, { schema: HTTP_SCHEMA, error: "not_found" }); return; }
    const body = await readBody(request, maxBodyBytes);
    if (request.url === "/mode") {
      const value = strictObject(body, ["mode"]);
      if (value.mode !== "human" && value.mode !== "shadow" && value.mode !== "one_step" && value.mode !== "auto") throw new Error("mode is invalid");
      const status = await runtime.setMode(value.mode);
      if (value.mode === "auto" || value.mode === "shadow") ensureAutoWorker();
      json(response, 200, { schema: HTTP_SCHEMA, status });
      return;
    }
    if (request.url === "/stop") {
      strictObject(body, []);
      json(response, 200, { schema: HTTP_SCHEMA, status: await runtime.stop() });
      return;
    }
    const value = body === undefined ? {} : strictObject(body, ["max_ticks"]);
    const requestedValue = value.max_ticks;
    const requested = requestedValue === undefined ? 1 : requestedValue;
    if (typeof requested !== "number" || !Number.isSafeInteger(requested) || requested < 1 || requested > maxAutoTicks) throw new Error(`max_ticks must be between 1 and ${maxAutoTicks}`);
    const limit = runtime.status().mode === "one_step" ? 1 : requested;
    const results: TickResult[] = [];
    for (let index = 0; index < limit; index += 1) {
      const result = await runtime.tick();
      results.push(result);
      if (result.type === "unknown" || runtime.status().mode === "human" || runtime.status().tainted) break;
    }
    json(response, 200, { schema: `${HTTP_SCHEMA}/tick-1`, results, status: runtime.status() });
  } catch (error) {
    const status = error instanceof Error && error.message.includes("body") ? 413 : 400;
    json(response, status, { schema: HTTP_SCHEMA, error: error instanceof Error ? error.message : String(error) });
  }
}

function isDrivenMode(mode: string): boolean { return mode === "auto" || mode === "shadow"; }

async function readBody(request: IncomingMessage, maxBytes: number): Promise<Record<string, unknown> | undefined> {
  let bytes = 0;
  const chunks: Buffer[] = [];
  for await (const chunk of request) {
    const data = Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk);
    bytes += data.byteLength;
    if (bytes > maxBytes) throw new Error("request body exceeds configured body limit");
    chunks.push(data);
  }
  if (chunks.length === 0) return undefined;
  const value: unknown = JSON.parse(Buffer.concat(chunks).toString("utf8"));
  if (value === null || typeof value !== "object" || Array.isArray(value)) throw new Error("request body must be a JSON object");
  return value as Record<string, unknown>;
}

function strictObject(value: Record<string, unknown> | undefined, keys: string[]): Record<string, unknown> {
  if (value === undefined) { if (keys.length === 0) return {}; throw new Error("request body is required"); }
  const expected = new Set(keys);
  const actual = Object.keys(value);
  if (actual.length !== expected.size || actual.some((key) => !expected.has(key))) throw new Error("request body has unknown or missing fields");
  return value;
}

function json(response: ServerResponse, statusCode: number, value: unknown): void {
  const body = `${JSON.stringify(value)}\n`;
  response.writeHead(statusCode, { "content-type": "application/json", "content-length": Buffer.byteLength(body), "cache-control": "no-store" });
  response.end(body);
}
