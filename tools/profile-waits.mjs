#!/usr/bin/env node
import { readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { summarizeCausalWaits } from "../src/causal-wait-profiler.mjs";

function option(args, name, fallback = null) {
  const index = args.indexOf(name);
  return index >= 0 && args[index + 1] ? args[index + 1] : fallback;
}

function readJsonl(file) {
  return readFileSync(file, "utf8").split(/\r?\n/u).filter(Boolean).map((line) => JSON.parse(line));
}

const args = process.argv.slice(2);
const eventsFile = args.find((arg) => !arg.startsWith("--"));
if (!eventsFile) {
  throw new Error("Usage: node tools/profile-waits.mjs EVENTS.jsonl [--output profile.json]");
}
const resolvedEvents = path.resolve(eventsFile);
const profile = {
  generated_at: new Date().toISOString(),
  source_events_file: resolvedEvents,
  ...summarizeCausalWaits(readJsonl(resolvedEvents))
};
const outputFile = option(args, "--output");
if (outputFile) {
  const resolvedOutput = path.resolve(outputFile);
  writeFileSync(resolvedOutput, `${JSON.stringify(profile, null, 2)}\n`);
  console.log(resolvedOutput);
} else {
  console.log(JSON.stringify(profile, null, 2));
}
