#!/usr/bin/env node
import { createWorkbenchService, SERVICE_NAMES } from "../src/workbench-service.mjs";
import { createWorkbenchServer } from "../src/server.mjs";
import { pathToFileURL } from "node:url";

function usage() {
  return `Usage: sts2-workbench [options]

Options:
  --root <name=path>       Configure one service root (repeatable)
  --<name>-root <path>     Configure a named root
  --host <host>            Bind address (default: 127.0.0.1)
  --port <port>            TCP port (default: 8787; 0 selects an ephemeral port)
  --help                   Show this message

Service names: ${SERVICE_NAMES.join(", ")}
Environment variables: WORKBENCH_<NAME>_ROOT
`;
}

function requireValue(args, index, option) {
  const value = args[index + 1];
  if (!value || value.startsWith("--")) throw new Error(`${option} requires a value`);
  return value;
}

export function parseArgs(args, env = process.env) {
  const roots = {};
  for (const name of SERVICE_NAMES) {
    const envName = `WORKBENCH_${name.toUpperCase()}_ROOT`;
    if (env[envName]) roots[name] = env[envName];
  }
  let host = "127.0.0.1";
  let port = 8787;

  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];
    if (argument === "--help") return { help: true };
    if (argument === "--host") {
      host = requireValue(args, index, argument);
      index += 1;
      continue;
    }
    if (argument === "--port") {
      const value = requireValue(args, index, argument);
      port = Number(value);
      if (!Number.isInteger(port) || port < 0 || port > 65535) {
        throw new Error("--port must be an integer from 0 to 65535");
      }
      index += 1;
      continue;
    }
    if (argument === "--root") {
      const assignment = requireValue(args, index, argument);
      const separator = assignment.indexOf("=");
      const name = separator === -1 ? "" : assignment.slice(0, separator);
      const value = separator === -1 ? "" : assignment.slice(separator + 1);
      if (!SERVICE_NAMES.includes(name) || value === "") {
        throw new Error("--root must use <service=path>");
      }
      roots[name] = value;
      index += 1;
      continue;
    }
    const namedRoot = /^(--(environment|annotator|evidence|transfer|diagnostics)-root)$/u.exec(argument);
    if (namedRoot) {
      roots[namedRoot[2]] = requireValue(args, index, argument);
      index += 1;
      continue;
    }
    throw new Error(`unknown option: ${argument}`);
  }
  return { help: false, host, port, roots };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const options = parseArgs(process.argv.slice(2));
    if (options.help) {
      process.stdout.write(usage());
      process.exit(0);
    }
    const service = createWorkbenchService(options.roots);
    const server = createWorkbenchServer(service);
    server.listen(options.port, options.host, () => {
      const address = server.address();
      const port = typeof address === "object" && address !== null ? address.port : options.port;
      process.stdout.write(`STS2 Workbench listening at http://${options.host}:${port}\n`);
      process.stdout.write("Read-only status API: /api/status\n");
    });
    const close = () => server.close(() => process.exit(0));
    process.once("SIGINT", close);
    process.once("SIGTERM", close);
  } catch (error) {
    process.stderr.write(`${error.message}\n\n${usage()}`);
    process.exitCode = 2;
  }
}
