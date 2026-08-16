#!/usr/bin/env node
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  discoverGameDirectory,
  readDiskIdentity,
  resolveInstallation
} from "../src/game-installation.mjs";
import {
  installConnectorRelease,
  rollbackConnectorRelease,
  writeInstallationRecord
} from "../src/connector-release.mjs";
import {
  queryHeadlessStatus,
  runHeadlessHost,
  stopHeadlessHost
} from "../src/headless-host.mjs";
import { listGameProcesses, runShippedProbe } from "../src/runtime-probe.mjs";
import { runBoundedJourney } from "../src/journey-probe.mjs";
import { evaluateRuntimeCompatibility } from "../src/compatibility.mjs";
import { enableConnectorModLoading, resetIsolatedProfile } from "../src/profile-isolation.mjs";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

function option(args, name, fallback) {
  const index = args.indexOf(name);
  return index >= 0 && args[index + 1] ? args[index + 1] : fallback;
}

function resolveCurrentInstallation() {
  const gameDir = discoverGameDirectory();
  if (!gameDir) throw new Error("Could not locate the Steam installation; set STS2_GAME_DIR.");
  return resolveInstallation(gameDir);
}

async function main() {
  const [command = "help", ...args] = process.argv.slice(2);
  if (command === "setup") {
    const installation = resolveCurrentInstallation();
    const localRoot = path.join(ROOT, ".local", "releases");
    const result = await installConnectorRelease({ installation, localRoot });
    writeInstallationRecord(path.join(ROOT, ".local", "last-connector-install.json"), result);
    console.log(JSON.stringify(result, null, 2));
    return;
  }
  if (command === "rollback") {
    const backup = option(args, "--backup", null);
    if (!backup) throw new Error("rollback requires --backup <directory>.");
    const result = rollbackConnectorRelease({
      backup,
      localRoot: path.join(ROOT, ".local", "releases")
    });
    console.log(JSON.stringify(result, null, 2));
    return;
  }
  if (command === "doctor") {
    const installation = resolveCurrentInstallation();
    const diskIdentity = readDiskIdentity(installation);
    const compatibility = evaluateRuntimeCompatibility(diskIdentity);
    const runningGameProcesses = listGameProcesses();
    const report = {
      schema_version: 1,
      generated_at: new Date().toISOString(),
      node: process.version,
      disk_identity: diskIdentity,
      compatibility,
      running_game_processes: runningGameProcesses,
      ready_for_supported_start:
        runningGameProcesses.length === 0 && compatibility.status === "supported_exact",
      ready_for_experimental_probe: runningGameProcesses.length === 0
    };
    console.log(JSON.stringify(report, null, 2));
    process.exitCode = report.ready_for_supported_start ? 0 : 2;
    return;
  }
  if (command === "start") {
    const installation = resolveCurrentInstallation();
    await runHeadlessHost({
      installation,
      localRoot: path.join(ROOT, ".local"),
      endpoint: option(args, "--endpoint", "http://127.0.0.1:15526"),
      timeoutMs: Number(option(args, "--timeout-ms", "90000")),
      mirrorLogs: args.includes("--verbose"),
      sharedProfileAcknowledged: args.includes("--shared-profile"),
      isolatedProfileId: option(args, "--isolated-profile", null)
    });
    return;
  }
  if (command === "status") {
    console.log(JSON.stringify(await queryHeadlessStatus({
      localRoot: path.join(ROOT, ".local"),
      endpoint: option(args, "--endpoint", "http://127.0.0.1:15526")
    }), null, 2));
    return;
  }
  if (command === "stop") {
    console.log(JSON.stringify(await stopHeadlessHost({
      localRoot: path.join(ROOT, ".local"),
      endpoint: option(args, "--endpoint", "http://127.0.0.1:15526")
    }), null, 2));
    return;
  }
  if (command === "reset-profile") {
    const profileId = option(args, "--isolated-profile", null);
    if (!profileId) throw new Error("reset-profile requires --isolated-profile <id>.");
    const running = listGameProcesses();
    if (running.length > 0) {
      throw new Error(`Refusing to reset a profile while STS2 is running:\n${running.join("\n")}`);
    }
    console.log(JSON.stringify(resetIsolatedProfile(path.join(ROOT, ".local"), profileId), null, 2));
    return;
  }
  if (command === "enable-profile-mods") {
    const profileId = option(args, "--isolated-profile", null);
    const expectedSettingsSchema = Number(option(args, "--settings-schema", ""));
    if (!profileId) throw new Error("enable-profile-mods requires --isolated-profile <id>.");
    const running = listGameProcesses();
    if (running.length > 0) {
      throw new Error(`Refusing to rewrite profile settings while STS2 is running:\n${running.join("\n")}`);
    }
    console.log(JSON.stringify(enableConnectorModLoading(path.join(ROOT, ".local"), profileId, {
      expectedSettingsSchema,
      acknowledgeEarlyAccessDisclaimer: args.includes("--accept-ea-disclaimer")
    }), null, 2));
    return;
  }
  if (command === "probe-shipped" || command === "probe-menu-control") {
    const installation = resolveCurrentInstallation();
    const timeoutMs = Number(option(args, "--timeout-ms", "90000"));
    const endpoint = option(args, "--endpoint", "http://127.0.0.1:15526");
    const result = await runShippedProbe({
      installation,
      localRoot: path.join(ROOT, ".local"),
      endpoint,
      timeoutMs,
      evidenceRoot: path.join(ROOT, ".local", "evidence"),
      exerciseMenu: command === "probe-menu-control",
      sharedProfileAcknowledged: args.includes("--shared-profile"),
      isolatedProfileId: option(args, "--isolated-profile", null),
      experimentalBuildAcknowledged: args.includes("--experimental-build")
    });
    console.log(JSON.stringify({
      report_file: result.reportFile,
      evidence_directory: result.evidenceDir,
      boot_verdict: result.report.verdict,
      control_gate_verdict: result.report.control_gate?.verdict ?? null
    }, null, 2));
    const passed = command === "probe-menu-control"
      ? result.report.control_gate?.verdict?.verdict === "h1_pass"
      : result.report.verdict.verdict === "h0_pass";
    process.exitCode = passed ? 0 : 3;
    return;
  }
  if (command === "probe-journey") {
    const result = await runBoundedJourney({
      installation: resolveCurrentInstallation(),
      localRoot: path.join(ROOT, ".local"),
      endpoint: option(args, "--endpoint", "http://127.0.0.1:15526"),
      timeoutMs: Number(option(args, "--timeout-ms", "90000")),
      actionTimeoutMs: Number(option(args, "--action-timeout-ms", "20000")),
      maxActions: Number(option(args, "--max-actions", "40")),
      tutorialPreference: option(args, "--tutorials", "disable"),
      evidenceRoot: path.join(ROOT, ".local", "evidence"),
      sharedProfileAcknowledged: args.includes("--shared-profile"),
      isolatedProfileId: option(args, "--isolated-profile", null),
      experimentalBuildAcknowledged: args.includes("--experimental-build")
    });
    console.log(JSON.stringify({
      report_file: result.reportFile,
      events_file: result.eventsFile,
      verdict: result.report.verdict
    }, null, 2));
    process.exitCode = result.report.verdict.verdict === "h2_pass" ? 0 : 4;
    return;
  }
  console.log(`Usage:
  node tools/headless.mjs setup
  node tools/headless.mjs rollback --backup DIRECTORY
  node tools/headless.mjs doctor
  node tools/headless.mjs start (--isolated-profile ID | --shared-profile) [--timeout-ms 90000] [--endpoint URL] [--verbose]
  node tools/headless.mjs status [--endpoint URL]
  node tools/headless.mjs stop [--endpoint URL]
  node tools/headless.mjs reset-profile --isolated-profile ID
  node tools/headless.mjs enable-profile-mods --isolated-profile ID --settings-schema VERSION [--accept-ea-disclaimer]
  node tools/headless.mjs probe-shipped (--isolated-profile ID | --shared-profile) [--experimental-build] [--timeout-ms 90000] [--endpoint URL]
  node tools/headless.mjs probe-menu-control (--isolated-profile ID | --shared-profile) [--experimental-build] [--timeout-ms 90000] [--endpoint URL]
  node tools/headless.mjs probe-journey (--isolated-profile ID | --shared-profile) [--experimental-build] [--max-actions 40] [--tutorials disable|enable] [--timeout-ms 90000]`);
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
});
