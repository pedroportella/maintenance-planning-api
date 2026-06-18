#!/usr/bin/env node

import { execFileSync } from "node:child_process";

const imageName = process.env.IMAGE_NAME ?? "maintenance-planning-api:local";
const repository = process.env.IMAGE_REPOSITORY ?? "maintenance-planning-api";
const revision = process.env.SOURCE_REVISION ?? currentGitRevision();

const buildArgs = [
  "build",
  "--pull",
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
