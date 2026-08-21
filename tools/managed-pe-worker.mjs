#!/usr/bin/env node
import { readFileSync } from "node:fs";
import { readDiskIdentity, resolveInstallation } from "../src/game-installation.mjs";
import { runManagedPlayerEnvironmentProbe } from "../src/managed-player-environment-probe.mjs";

const configFile = process.argv[2];
if (configFile == null) throw new Error("managed-pe-worker requires a config file.");
const config = JSON.parse(readFileSync(configFile, "utf8"));
const cpuStarted = process.cpuUsage();
const startedAt = Date.now();
const result = await runManagedPlayerEnvironmentProbe({
  ...config.probe,
  diskIdentity: readDiskIdentity(resolveInstallation(config.gameDirectory)),
  evidenceRoot: null
});
const cpu = process.cpuUsage(cpuStarted);
const memory = process.memoryUsage();
process.stdout.write(`${JSON.stringify({
  report: result.report,
  worker_node: {
    pid: process.pid,
    wall_seconds: (Date.now() - startedAt) / 1000,
    cpu_seconds: (cpu.user + cpu.system) / 1_000_000,
    final_rss_bytes: memory.rss,
    final_heap_used_bytes: memory.heapUsed
  }
})}\n`);
