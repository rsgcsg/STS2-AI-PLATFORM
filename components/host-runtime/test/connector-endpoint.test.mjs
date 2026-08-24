import assert from "node:assert/strict";
import test from "node:test";
import {
  CONNECTOR_PORT_ENVIRONMENT_VARIABLE,
  GAME_CANARY_ENVIRONMENT_VARIABLE,
  HOST_CONTROL_TOKEN_ENVIRONMENT_VARIABLE,
  resolveConnectorEndpoint,
  SOURCE_CANARY_ENVIRONMENT_VARIABLE
} from "../src/connector-endpoint.mjs";

test("binds one explicit loopback endpoint to process-local Connector config", () => {
  assert.deepEqual(resolveConnectorEndpoint("http://127.0.0.1:16001"), {
    endpoint: "http://127.0.0.1:16001",
    port: 16001,
    process_environment: { [CONNECTOR_PORT_ENVIRONMENT_VARIABLE]: "16001" }
  });
  assert.equal(resolveConnectorEndpoint("http://localhost:16002").port, 16002);
  assert.equal(HOST_CONTROL_TOKEN_ENVIRONMENT_VARIABLE, "STS2_CONNECTOR_HOST_CONTROL_TOKEN");
  assert.equal(GAME_CANARY_ENVIRONMENT_VARIABLE, "STS2_CONNECTOR_EXPERIMENTAL_GAME_ID");
  assert.equal(SOURCE_CANARY_ENVIRONMENT_VARIABLE, "STS2_CONNECTOR_EXPERIMENTAL_SOURCE_REVISION");
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
