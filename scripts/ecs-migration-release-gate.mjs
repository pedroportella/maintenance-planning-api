#!/usr/bin/env node

import { readFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import {
  evaluateRunTaskResult,
  evaluateStoppedTask,
  validateTaskDefinitionPayload
} from "./ecs-task-result.mjs";

const args = parseArgs(process.argv.slice(2));
const dryRun = Boolean(args.dryRun);
const migrationContainer = args.migrationContainer ?? process.env.MIGRATION_CONTAINER_NAME ?? "migration-runner";
const releaseId = args.releaseId ?? process.env.RELEASE_ID ?? currentGitRevision();
const cluster = args.cluster ?? process.env.ECS_CLUSTER_ARN ?? process.env.ECS_CLUSTER_NAME;
const service = args.service ?? process.env.ECS_API_SERVICE_NAME;
const subnets = splitList(args.subnets ?? process.env.ECS_PRIVATE_SUBNET_IDS);
const securityGroups = splitList(args.securityGroups ?? process.env.ECS_MIGRATION_SECURITY_GROUP_IDS);
const assignPublicIp = args.assignPublicIp ?? process.env.ECS_ASSIGN_PUBLIC_IP ?? "DISABLED";
const apiTaskDefinitionPath = args.apiTaskDefinition ?? process.env.API_TASK_DEFINITION_FILE;
const migrationTaskDefinitionPath = args.migrationTaskDefinition ?? process.env.MIGRATION_TASK_DEFINITION_FILE;

const apiTaskDefinition = readTaskDefinition(apiTaskDefinitionPath, "api task definition");
const migrationTaskDefinition = readTaskDefinition(migrationTaskDefinitionPath, "migration task definition");
const validationFailures = [
  ...validateTaskDefinitionPayload(apiTaskDefinition, { label: "api task definition" }),
  ...validateTaskDefinitionPayload(migrationTaskDefinition, {
    label: "migration task definition",
    requiredContainerName: migrationContainer
  }),
  ...validateReleaseInputs()
];

if (validationFailures.length > 0) {
  console.error("ECS migration release gate validation failed:");
  for (const failure of validationFailures) {
    console.error(`- ${failure}`);
  }
  process.exit(1);
}

if (dryRun) {
  console.log(`Dry-run validated release ${releaseId}.`);
  console.log(`API task family: ${apiTaskDefinition.family}`);
  console.log(`Migration task family: ${migrationTaskDefinition.family}`);
  console.log(`Migration container: ${migrationContainer}`);
  console.log(`Network: ${subnets.length} private subnet(s), ${securityGroups.length} security group(s), assignPublicIp=${assignPublicIp}`);
  process.exit(0);
}

const migrationTaskDefinitionArn = registerTaskDefinition(migrationTaskDefinition, "migration");
const apiTaskDefinitionArn = registerTaskDefinition(apiTaskDefinition, "api");
const migrationTaskArn = runMigrationTask(migrationTaskDefinitionArn);

waitForTaskStopped(migrationTaskArn);

const stoppedTask = describeTask(migrationTaskArn);
const migrationResult = evaluateStoppedTask(stoppedTask, migrationContainer);
if (!migrationResult.ok) {
  throw new Error(`Migration task failed for release ${releaseId}: ${migrationResult.reason}`);
}

console.log(`Migration task succeeded for release ${releaseId}: ${migrationTaskArn}.`);
updateService(apiTaskDefinitionArn);
waitForServiceStable();
console.log(`API service updated for release ${releaseId}: ${apiTaskDefinitionArn}.`);

function readTaskDefinition(filePath, label) {
  if (!filePath) {
    throw new Error(`Missing ${label} file. Pass --${label.replaceAll(" ", "-")} or set the matching environment variable.`);
  }

  return JSON.parse(readFileSync(filePath, "utf8"));
}

function validateReleaseInputs() {
  const failures = [];

  if (!cluster) failures.push("ECS cluster is required.");
  if (!service) failures.push("ECS API service name is required.");
  if (subnets.length === 0) failures.push("At least one private subnet is required.");
  if (securityGroups.length === 0) failures.push("At least one migration security group is required.");
  if (!["DISABLED", "ENABLED"].includes(assignPublicIp)) failures.push("assignPublicIp must be DISABLED or ENABLED.");
  if (assignPublicIp !== "DISABLED") failures.push("Migration tasks must run with assignPublicIp=DISABLED.");

  return failures;
}

function registerTaskDefinition(taskDefinition, label) {
  const response = aws([
    "ecs",
    "register-task-definition",
    "--cli-input-json",
    JSON.stringify(taskDefinition),
    "--output",
    "json"
  ]);
  const taskDefinitionArn = response?.taskDefinition?.taskDefinitionArn;
  if (!taskDefinitionArn) {
    throw new Error(`Could not register ${label} task definition.`);
  }

  console.log(`Registered ${label} task definition ${taskDefinitionArn}.`);
  return taskDefinitionArn;
}

function runMigrationTask(taskDefinitionArn) {
  const networkConfiguration = {
    awsvpcConfiguration: {
      subnets,
      securityGroups,
      assignPublicIp
    }
  };
  const response = aws([
    "ecs",
    "run-task",
    "--cluster",
    cluster,
    "--task-definition",
    taskDefinitionArn,
    "--launch-type",
    "FARGATE",
    "--started-by",
    `release-${releaseId}`.slice(0, 36),
    "--network-configuration",
    JSON.stringify(networkConfiguration),
    "--output",
    "json"
  ]);
  const result = evaluateRunTaskResult(response);
  if (!result.ok) {
    throw new Error(`Could not start migration task for release ${releaseId}: ${result.reason}`);
  }

  console.log(`Started migration task ${result.taskArn}.`);
  return result.taskArn;
}

function waitForTaskStopped(taskArn) {
  aws([
    "ecs",
    "wait",
    "tasks-stopped",
    "--cluster",
    cluster,
    "--tasks",
    taskArn
  ]);
}

function describeTask(taskArn) {
  return aws([
    "ecs",
    "describe-tasks",
    "--cluster",
    cluster,
    "--tasks",
    taskArn,
    "--output",
    "json"
  ]);
}

function updateService(apiTaskDefinitionArn) {
  aws([
    "ecs",
    "update-service",
    "--cluster",
    cluster,
    "--service",
    service,
    "--task-definition",
    apiTaskDefinitionArn,
    "--force-new-deployment",
    "--output",
    "json"
  ]);
}

function waitForServiceStable() {
  aws([
    "ecs",
    "wait",
    "services-stable",
    "--cluster",
    cluster,
    "--services",
    service
  ]);
}

function aws(commandArgs) {
  const output = execFileSync("aws", commandArgs, {
    encoding: "utf8",
    stdio: ["ignore", "pipe", "inherit"]
  });

  return output.trim() ? JSON.parse(output) : {};
}

function parseArgs(argv) {
  const parsed = {};

  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (arg === "--dry-run") {
      parsed.dryRun = true;
      continue;
    }

    if (!arg.startsWith("--")) {
      throw new Error(`Unexpected argument: ${arg}`);
    }

    const key = toCamelCase(arg.slice(2));
    const value = argv[index + 1];
    if (!value || value.startsWith("--")) {
      throw new Error(`Missing value for ${arg}.`);
    }

    parsed[key] = value;
    index += 1;
  }

  return parsed;
}

function splitList(value) {
  return (value ?? "")
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

function toCamelCase(value) {
  return value.replaceAll(/-([a-z])/g, (_, character) => character.toUpperCase());
}

function currentGitRevision() {
  try {
    return execFileSync("git", ["rev-parse", "--short=12", "HEAD"], {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"]
    }).trim();
  } catch {
    return "local";
  }
}
