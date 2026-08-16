import assert from "node:assert/strict";
import test from "node:test";
import {
  CONNECTOR_PORT_ENVIRONMENT_VARIABLE,
  resolveConnectorEndpoint
} from "../src/connector-endpoint.mjs";

test("binds one explicit loopback endpoint to process-local Connector config", () => {
  assert.deepEqual(resolveConnectorEndpoint("http://127.0.0.1:16001"), {
    endpoint: "http://127.0.0.1:16001",
    port: 16001,
    process_environment: { [CONNECTOR_PORT_ENVIRONMENT_VARIABLE]: "16001" }
  });
  assert.equal(resolveConnectorEndpoint("http://localhost:16002").port, 16002);
});

test("rejects ambiguous, remote, implicit, and path-bearing endpoints", () => {
  for (const value of [
    "not-a-url",
    "https://127.0.0.1:16001",
    "http://192.168.1.20:16001",
    "http://127.0.0.1",
    "http://127.0.0.1:16001/api"
  ]) {
    assert.throws(() => resolveConnectorEndpoint(value), /Connector endpoint/u);
  }
});
