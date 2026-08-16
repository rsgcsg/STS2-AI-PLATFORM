export const CONNECTOR_PORT_ENVIRONMENT_VARIABLE = "STS2_CONNECTOR_PORT";
export const HOST_CONTROL_TOKEN_ENVIRONMENT_VARIABLE =
  "STS2_CONNECTOR_HOST_CONTROL_TOKEN";
export const GAME_CANARY_ENVIRONMENT_VARIABLE =
  "STS2_CONNECTOR_EXPERIMENTAL_GAME_ID";
export const SOURCE_CANARY_ENVIRONMENT_VARIABLE =
  "STS2_CONNECTOR_EXPERIMENTAL_SOURCE_REVISION";

const LOOPBACK_HOSTS = new Set(["127.0.0.1", "localhost", "[::1]"]);

export function resolveConnectorEndpoint(endpoint) {
  let parsed;
  try {
    parsed = new URL(endpoint);
  } catch {
    throw new Error(`Invalid Connector endpoint: ${endpoint}`);
  }
  if (parsed.protocol !== "http:"
      || !LOOPBACK_HOSTS.has(parsed.hostname)
      || parsed.username
      || parsed.password
      || parsed.pathname !== "/"
      || parsed.search
      || parsed.hash
      || parsed.port === "") {
    throw new Error(
      "Connector endpoints must be explicit localhost HTTP origins such as http://127.0.0.1:15526."
    );
  }
  const port = Number.parseInt(parsed.port, 10);
  if (!Number.isSafeInteger(port) || port <= 0 || port > 65535) {
    throw new Error(`Invalid Connector endpoint port: ${parsed.port}`);
  }
  return {
    endpoint: parsed.origin,
    port,
    process_environment: {
      [CONNECTOR_PORT_ENVIRONMENT_VARIABLE]: String(port)
    }
  };
}
