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
  "docs/production-next.md",
  "docs/release-gate.md",
  "docs/reviewer-runbook.md",
  "docs/security-and-operations.md",
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
  "docs/security-and-operations.md",
  "synthetic"
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
