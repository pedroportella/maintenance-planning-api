#!/usr/bin/env node

import { execFileSync } from "node:child_process";

const imageName = process.env.MIGRATION_IMAGE_NAME ?? "maintenance-planning-migration-runner:local";
const repository = process.env.MIGRATION_IMAGE_REPOSITORY ?? "maintenance-planning-migration-runner";
const revision = process.env.SOURCE_REVISION ?? currentGitRevision();

const buildArgs = [
  "build",
  "--pull",
  "--file",
  "Dockerfile.migrations",
  "--build-arg",
  `IMAGE_REPOSITORY=${repository}`,
  "--build-arg",
  `VCS_REF=${revision}`,
  "--label",
  `org.opencontainers.image.revision=${revision}`,
  "--label",
  `org.opencontainers.image.source=${repository}`,
  "-t",
  imageName,
  "."
];

console.log(`Building ${imageName} with revision ${revision}.`);
execFileSync("docker", buildArgs, { stdio: "inherit" });

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
