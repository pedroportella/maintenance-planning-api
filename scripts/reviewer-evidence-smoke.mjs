#!/usr/bin/env node

import { existsSync, readFileSync, statSync } from "node:fs";
import { dirname, join, normalize } from "node:path";

const requiredFiles = [
  "README.md",
  "AGENTS.md",
  ".cursorrules",
  ".env.local.example",
  "docs/architecture.md",
  "docs/api.md",
  "docs/aws-terraform.md",
  "docs/containerisation.md",
  "docs/event-contracts.md",
  "docs/outbound-events.asyncapi.json",
  "docs/production-next.md",
  "docs/release-gate.md",
  "docs/reviewer-runbook.md",
  "docs/runtime-upgrade-policy.md",
  "docs/security-and-operations.md",
  "contracts/planning-run-completed.schema.json",
  "contracts/package-decision-recorded.schema.json",
  "scripts/event-contract-smoke.mjs",
  "scripts/env-loader.mjs"
];

const requiredReadmeText = [
  "docs/architecture.md",
  "docs/api.md",
  "docs/aws-terraform.md",
  "docs/containerisation.md",
  "docs/event-contracts.md",
  "docs/release-gate.md",
  "docs/production-next.md",
  "docs/reviewer-runbook.md",
  "docs/runtime-upgrade-policy.md",
  "docs/security-and-operations.md",
  "synthetic"
];

const requiredRuntimePolicyText = [
  { filePath: "Directory.Build.props", expected: "<TargetFramework>net8.0</TargetFramework>" },
  { filePath: "global.json", expected: "\"rollForward\": \"latestPatch\"" },
  { filePath: ".config/dotnet-tools.json", expected: "\"version\": \"8.0.28\"" },
  { filePath: "Dockerfile", expected: "ARG DOTNET_IMAGE_TAG=8.0-bookworm-slim" },
  { filePath: "Dockerfile.worker", expected: "ARG DOTNET_IMAGE_TAG=8.0-bookworm-slim" },
  { filePath: "Dockerfile.migrations", expected: "ARG DOTNET_IMAGE_TAG=8.0-bookworm-slim" },
  { filePath: "docs/runtime-upgrade-policy.md", expected: "`net8.0`" },
  { filePath: "docs/runtime-upgrade-policy.md", expected: "`latestPatch`" },
  { filePath: "docs/runtime-upgrade-policy.md", expected: "`DOTNET_IMAGE_TAG=8.0-bookworm-slim`" }
];

const failures = [];

for (const filePath of requiredFiles) {
  if (!existsSync(filePath)) {
    failures.push(`required file is missing: ${filePath}`);
  }
}

if (existsSync("README.md")) {
  const readme = readFileSync("README.md", "utf8");
  for (const expected of requiredReadmeText) {
    if (!readme.includes(expected)) {
      failures.push(`README.md is missing expected reviewer evidence: ${expected}`);
    }
  }
}

for (const { filePath, expected } of requiredRuntimePolicyText) {
  if (!existsSync(filePath)) {
    failures.push(`runtime policy check cannot find: ${filePath}`);
    continue;
  }

  const contents = readFileSync(filePath, "utf8");
  if (!contents.includes(expected)) {
    failures.push(`${filePath} is missing expected runtime policy text: ${expected}`);
  }
}

for (const filePath of requiredFiles.filter((path) => path.endsWith(".md") && existsSync(path))) {
  const contents = readFileSync(filePath, "utf8");
  for (const target of extractMarkdownLinkTargets(contents)) {
    checkMarkdownLink(filePath, target);
  }
}

if (failures.length > 0) {
  console.error("Reviewer evidence smoke failed:");
  for (const failure of failures) {
    console.error(`- ${failure}`);
  }
  process.exitCode = 1;
} else {
  console.log("Reviewer evidence smoke passed.");
}

function extractMarkdownLinkTargets(contents) {
  const targets = [];
  const markdownLinkPattern = /!?\[[^\]]*]\(([^)]+)\)/g;
  let match;

  while ((match = markdownLinkPattern.exec(contents)) !== null) {
    const rawTarget = match[1].trim().split(/\s+/)[0];
    targets.push(stripAngleBrackets(rawTarget));
  }

  return targets;
}

function stripAngleBrackets(value) {
  if (value.startsWith("<") && value.endsWith(">")) {
    return value.slice(1, -1);
  }

  return value;
}

function checkMarkdownLink(sourceFilePath, rawTarget) {
  if (rawTarget === "" || rawTarget.startsWith("#") || /^[a-z][a-z0-9+.-]*:/i.test(rawTarget)) {
    return;
  }

  const [pathWithoutAnchor] = rawTarget.split("#");
  if (pathWithoutAnchor === "") return;

  let decodedPath = pathWithoutAnchor;
  try {
    decodedPath = decodeURIComponent(pathWithoutAnchor);
  } catch {
    failures.push(`${sourceFilePath} has an invalid encoded Markdown link: ${rawTarget}`);
    return;
  }

  const resolvedPath = normalize(join(dirname(sourceFilePath), decodedPath));

  if (!existsSync(resolvedPath)) {
    failures.push(`${sourceFilePath} links to missing local target: ${rawTarget}`);
    return;
  }

  const stats = statSync(resolvedPath);
  if (!stats.isFile() && !stats.isDirectory()) {
    failures.push(`${sourceFilePath} links to unsupported local target: ${rawTarget}`);
  }
}
