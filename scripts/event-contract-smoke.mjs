#!/usr/bin/env node

import { existsSync, readFileSync } from "node:fs";

const requiredFiles = [
  "docs/event-contracts.md",
  "docs/outbound-events.asyncapi.json",
  "contracts/planning-run-completed.schema.json",
  "contracts/package-decision-recorded.schema.json"
];

const requiredEventTypes = [
  {
    eventType: "planning.run.completed",
    schemaPath: "contracts/planning-run-completed.schema.json",
    asyncApiMessage: "planningRunCompleted"
  },
  {
    eventType: "planning.package.decision-recorded",
    schemaPath: "contracts/package-decision-recorded.schema.json",
    asyncApiMessage: "packageDecisionRecorded"
  }
];

const failures = [];

for (const file of requiredFiles) {
  if (!existsSync(file)) {
    failures.push(`required event contract file is missing: ${file}`);
  }
}

const eventDocs = existsSync("docs/event-contracts.md")
  ? readFileSync("docs/event-contracts.md", "utf8")
  : "";
const asyncApi = readJson("docs/outbound-events.asyncapi.json");

for (const contract of requiredEventTypes) {
  const schema = readJson(contract.schemaPath);

  if (!eventDocs.includes(contract.eventType)) {
    failures.push(`docs/event-contracts.md is missing ${contract.eventType}`);
  }

  const message = asyncApi?.components?.messages?.[contract.asyncApiMessage];
  if (!message) {
    failures.push(`AsyncAPI descriptor is missing ${contract.asyncApiMessage}`);
  } else {
    if (message.name !== contract.eventType) {
      failures.push(`AsyncAPI message ${contract.asyncApiMessage} name does not match ${contract.eventType}`);
    }

    if (message.payload?.$ref !== `../${contract.schemaPath}`) {
      failures.push(`AsyncAPI message ${contract.asyncApiMessage} does not reference ${contract.schemaPath}`);
    }
  }

  if (schema?.properties?.eventType?.const !== contract.eventType) {
    failures.push(`${contract.schemaPath} does not lock eventType to ${contract.eventType}`);
  }

  if (schema?.properties?.sourceSystem?.const !== "maintenance-planning-api") {
    failures.push(`${contract.schemaPath} does not lock sourceSystem to maintenance-planning-api`);
  }

  if (schema?.properties?.schemaVersion?.const !== "1.0") {
    failures.push(`${contract.schemaPath} does not lock schemaVersion to 1.0`);
  }
}

if (failures.length > 0) {
  console.error("Event contract smoke failed:");
  for (const failure of failures) {
    console.error(`- ${failure}`);
  }
  process.exitCode = 1;
} else {
  console.log("Event contract smoke passed.");
}

function readJson(path) {
  if (!existsSync(path)) {
    return null;
  }

  try {
    return JSON.parse(readFileSync(path, "utf8"));
  } catch (error) {
    failures.push(`${path} is not valid JSON: ${error.message}`);
    return null;
  }
}
