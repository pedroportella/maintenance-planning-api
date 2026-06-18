#!/usr/bin/env node

import { execFileSync } from "node:child_process";

const imageName = process.env.IMAGE_NAME ?? process.argv[2] ?? "maintenance-planning-api:local";
const hostPort = process.env.HOST_PORT ?? "5000";
const containerName = process.env.CONTAINER_NAME ?? "maintenance-planning-api-local";

const runArgs = [
  "run",
  "--rm",
  "--name",
  containerName,
  "-p",
  `${hostPort}:8080`,
  imageName
];

console.log(`Running ${imageName} at http://localhost:${hostPort}.`);
execFileSync("docker", runArgs, { stdio: "inherit" });
