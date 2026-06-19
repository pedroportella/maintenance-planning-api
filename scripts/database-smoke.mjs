#!/usr/bin/env node

import { execFileSync, spawn } from "node:child_process";
import { createServer } from "node:net";
import { loadLocalEnv } from "./env-loader.mjs";

const localEnv = loadLocalEnv();
const sqlPassword = localEnv.MSSQL_SA_PASSWORD ?? "LocalOnly_Passw0rd_123!";
const sqlPort = localEnv.MSSQL_HOST_PORT ?? String(await findFreePort());
const apiPort = localEnv.API_HOST_PORT ?? String(await findFreePort());
const composeProjectName = localEnv.COMPOSE_PROJECT_NAME ?? `maintenance-planning-api-smoke-${Date.now()}`;
const reviewerToken = "local-reviewer-token";

const databaseEnvironment = {
  ...localEnv,
  MSSQL_SA_PASSWORD: sqlPassword,
  MSSQL_HOST_PORT: sqlPort,
  MaintenancePlanning__Database__Enabled: "true",
  MaintenancePlanning__Database__Server: `127.0.0.1,${sqlPort}`,
  MaintenancePlanning__Database__Database: "MaintenancePlanning",
  MaintenancePlanning__Database__User: "sa",
  MaintenancePlanning__Database__Password: sqlPassword,
  MaintenancePlanning__Database__Encrypt: "true",
  MaintenancePlanning__Database__TrustServerCertificate: "true"
};

const composePrefix = ["compose", "--project-name", composeProjectName, "--profile", "sqlserver"];
let apiProcess;
let composeStarted = false;

try {
  execFileSync("dotnet", ["tool", "restore"], { stdio: "inherit" });
  execFileSync(
    "dotnet",
    [
      "build",
      "src/MaintenancePlanning.Infrastructure/MaintenancePlanning.Infrastructure.csproj",
      "--disable-build-servers",
      "-m:1",
      "-p:UseSharedCompilation=false"
    ],
    { stdio: "inherit" }
  );

  execFileSync("docker", [...composePrefix, "up", "-d", "sqlserver"], {
    env: databaseEnvironment,
    stdio: "inherit"
  });
  composeStarted = true;

  await applyMigrationsWhenReady();

  execFileSync(
    "dotnet",
    [
      "build",
      "src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj",
      "--disable-build-servers",
      "-m:1",
      "-p:UseSharedCompilation=false"
    ],
    { stdio: "inherit" }
  );

  apiProcess = spawn(
    "dotnet",
    [
      "run",
      "--no-build",
      "--project",
      "src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj",
      "--urls",
      `http://127.0.0.1:${apiPort}`
    ],
    {
      env: databaseEnvironment,
      stdio: ["ignore", "pipe", "pipe"]
    }
  );

  apiProcess.stdout.on("data", (chunk) => process.stdout.write(chunk));
  apiProcess.stderr.on("data", (chunk) => process.stderr.write(chunk));

  await waitForHttpOk(`http://127.0.0.1:${apiPort}/health/ready`, "API readiness");

  const migrationReadiness = await waitForJson(
    `http://127.0.0.1:${apiPort}/api/v1/operations/migration-readiness`,
    "migration readiness",
    { Authorization: `Bearer ${reviewerToken}` }
  );

  if (!migrationReadiness.databaseReachable || migrationReadiness.pendingMigrationCount !== 0) {
    throw new Error(`Unexpected migration readiness report: ${JSON.stringify(migrationReadiness)}`);
  }

  console.log("Database smoke passed.");
} finally {
  if (apiProcess) {
    apiProcess.kill("SIGTERM");
    await waitForProcessExit(apiProcess);
  }

  if (composeStarted) {
    execFileSync("docker", [...composePrefix, "down", "--volumes"], {
      env: databaseEnvironment,
      stdio: "inherit"
    });
  }
}

async function applyMigrationsWhenReady() {
  const deadline = Date.now() + 120_000;
  let lastError;

  while (Date.now() < deadline) {
    try {
      execFileSync(
        "dotnet",
        [
          "dotnet-ef",
          "database",
          "update",
          "--project",
          "src/MaintenancePlanning.Infrastructure/MaintenancePlanning.Infrastructure.csproj",
          "--startup-project",
          "src/MaintenancePlanning.Infrastructure/MaintenancePlanning.Infrastructure.csproj",
          "--context",
          "MaintenancePlanningDbContext",
          "--no-build"
        ],
        {
          env: databaseEnvironment,
          stdio: "pipe"
        }
      );

      console.log("Migrations applied.");
      return;
    } catch (error) {
      lastError = error;
      await delay(2_000);
    }
  }

  const stderr = lastError?.stderr?.toString("utf8").trim();
  throw new Error(`Could not apply migrations before timeout.${stderr ? ` Last error: ${stderr}` : ""}`);
}

async function waitForHttpOk(url, label) {
  const deadline = Date.now() + 60_000;
  let lastError;

  while (Date.now() < deadline) {
    try {
      const response = await fetch(url);
      if (response.ok) {
        console.log(`${label} passed.`);
        return;
      }

      lastError = new Error(`${url} returned ${response.status}`);
    } catch (error) {
      lastError = error;
    }

    await delay(500);
  }

  throw new Error(`${label} did not pass: ${lastError?.message ?? "timed out"}`);
}

async function waitForJson(url, label, headers = {}) {
  const deadline = Date.now() + 30_000;
  let lastError;

  while (Date.now() < deadline) {
    try {
      const response = await fetch(url, { headers });
      const body = await response.text();
      if (response.ok) {
        console.log(`${label} passed.`);
        return JSON.parse(body);
      }

      lastError = new Error(`${url} returned ${response.status}: ${body}`);
    } catch (error) {
      lastError = error;
    }

    await delay(500);
  }

  throw new Error(`${label} did not pass: ${lastError?.message ?? "timed out"}`);
}

function waitForProcessExit(childProcess) {
  if (childProcess.exitCode !== null) return Promise.resolve();

  return new Promise((resolve) => {
    childProcess.once("exit", resolve);
    setTimeout(resolve, 5_000).unref();
  });
}

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

function findFreePort() {
  return new Promise((resolve, reject) => {
    const server = createServer();
    server.on("error", reject);
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      const port = typeof address === "object" && address ? address.port : undefined;
      server.close(() => {
        if (!port) {
          reject(new Error("Could not reserve a host port."));
          return;
        }

        resolve(port);
      });
    });
  });
}
