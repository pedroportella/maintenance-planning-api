#!/usr/bin/env node

import { mkdirSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { dirname } from "node:path";

const options = parseArgs(process.argv.slice(2));
const output =
  options.output ?? process.env.MIGRATION_BUNDLE_OUTPUT ?? "artifacts/migration-bundle/MaintenancePlanning.Migrations";
const runtime = options.runtime ?? process.env.MIGRATION_BUNDLE_RUNTIME;
const configuration = options.configuration ?? process.env.CONFIGURATION ?? "Release";

mkdirSync(dirname(output), { recursive: true });

run("dotnet", ["tool", "restore"]);
run("dotnet", ["restore", "src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj"]);
run("dotnet", [
  "build",
  "src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj",
  "--configuration",
  configuration,
  "--no-restore",
  "--disable-build-servers",
  "-m:1",
  "-p:UseSharedCompilation=false"
]);
const bundleArgs = [
  "dotnet-ef",
  "migrations",
  "bundle",
  "--configuration",
  configuration,
  "--no-build",
  "--project",
  "src/MaintenancePlanning.Infrastructure/MaintenancePlanning.Infrastructure.csproj",
  "--startup-project",
  "src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj",
  "--context",
  "MaintenancePlanningDbContext",
  "--output",
  output,
  "--force"
];

if (runtime) {
  bundleArgs.splice(6, 0, "--target-runtime", runtime);
}

run("dotnet", bundleArgs);

console.log(`Migration bundle written to ${output}.`);

function run(command, args) {
  console.log(`${command} ${args.join(" ")}`);
  execFileSync(command, args, { stdio: "inherit" });
}

function parseArgs(args) {
  const parsed = {};

  for (let index = 0; index < args.length; index += 1) {
    const arg = args[index];
    if (!arg.startsWith("--")) {
      throw new Error(`Unexpected argument: ${arg}`);
    }

    const key = arg.slice(2);
    const value = args[index + 1];
    if (!value || value.startsWith("--")) {
      throw new Error(`Missing value for --${key}.`);
    }

    parsed[toCamelCase(key)] = value;
    index += 1;
  }

  return parsed;
}

function toCamelCase(value) {
  return value.replaceAll(/-([a-z])/g, (_, character) => character.toUpperCase());
}
