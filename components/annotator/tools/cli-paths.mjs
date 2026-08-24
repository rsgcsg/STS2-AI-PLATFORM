import path from "node:path";

export function resolveCliPath(value, cwd = process.cwd()) {
  return path.resolve(cwd, value);
}
