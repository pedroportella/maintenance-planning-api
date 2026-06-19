#!/usr/bin/env node

import assert from "node:assert/strict";
import {
  evaluateRunTaskResult,
  evaluateStoppedTask,
  validateTaskDefinitionPayload
} from "./ecs-task-result.mjs";

const digest = "sha256:" + "a".repeat(64);

assert.deepEqual(
  validateTaskDefinitionPayload(
    {
      family: "review-api",
      networkMode: "awsvpc",
      requiresCompatibilities: ["FARGATE"],
      containerDefinitions: [{ name: "api", image: `example.invalid/api@${digest}` }]
    },
    { label: "api" }
  ),
  []
);

assert.match(
  validateTaskDefinitionPayload(
    {
      family: "review-api",
      networkMode: "awsvpc",
      requiresCompatibilities: ["FARGATE"],
      containerDefinitions: [{ name: "api", image: "example.invalid/api:latest" }]
    },
    { label: "api" }
  ).join("\n"),
  /@sha256/
);

assert.match(
  validateTaskDefinitionPayload(
    {
      family: "review-migration",
      networkMode: "awsvpc",
      requiresCompatibilities: ["FARGATE"],
      containerDefinitions: [
        {
          name: "migration-runner",
          image: `example.invalid/migration@${digest}`,
          environment: [{ name: "MaintenancePlanning__Database__Password", value: "do-not-do-this" }]
        }
      ]
    },
    { label: "migration", requiredContainerName: "migration-runner" }
  ).join("\n"),
  /ECS secret/
);

assert.equal(evaluateRunTaskResult({ tasks: [{ taskArn: "task-1" }] }).taskArn, "task-1");
assert.match(evaluateRunTaskResult({ failures: [{ arn: "taskdef", reason: "RESOURCE:MEMORY" }] }).reason, /RESOURCE:MEMORY/);
assert.match(evaluateRunTaskResult({ tasks: [] }).reason, /no task ARN/);

assert.equal(
  evaluateStoppedTask(
    {
      tasks: [
        {
          taskArn: "task-1",
          stopCode: "EssentialContainerExited",
          containers: [{ name: "migration-runner", exitCode: 0 }]
        }
      ]
    },
    "migration-runner"
  ).ok,
  true
);

assert.match(
  evaluateStoppedTask(
    {
      tasks: [
        {
          taskArn: "task-1",
          stopCode: "TaskFailedToStart",
          stoppedReason: "CannotPullContainerError",
          containers: [{ name: "migration-runner" }]
        }
      ]
    },
    "migration-runner"
  ).reason,
  /TaskFailedToStart/
);

assert.match(
  evaluateStoppedTask(
    {
      tasks: [
        {
          taskArn: "task-1",
          stopCode: "EssentialContainerExited",
          containers: [{ name: "migration-runner", exitCode: 1, reason: "migration failed" }]
        }
      ]
    },
    "migration-runner"
  ).reason,
  /exited with 1/
);

assert.match(
  evaluateStoppedTask(
    {
      tasks: [
        {
          taskArn: "task-1",
          stopCode: "EssentialContainerExited",
          containers: [{ name: "migration-runner", reason: "still starting" }]
        }
      ]
    },
    "migration-runner"
  ).reason,
  /did not report an exit code/
);

console.log("ECS migration release gate parser tests passed.");
