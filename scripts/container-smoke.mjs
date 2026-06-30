#!/usr/bin/env node

import { execFileSync, spawnSync } from "node:child_process";
import { createServer } from "node:net";

const imageName = process.env.IMAGE_NAME ?? "maintenance-planning-api:local";
const shouldBuild = !process.argv.includes("--skip-build");
const hostPort = process.env.HOST_PORT ?? String(await findFreePort());
const containerName =
  process.env.CONTAINER_NAME ?? `maintenance-planning-api-smoke-${Date.now()}`;
const tokens = {
  plannerRead: "local-planner-read-token",
  plannerWrite: "local-planner-token",
  imports: "local-import-token",
  operations: "local-operations-token"
};

if (shouldBuild) {
  execFileSync("node", ["scripts/container-build.mjs"], {
    env: { ...process.env, IMAGE_NAME: imageName },
    stdio: "inherit"
  });
}

checkImageContents();

let containerStarted = false;

try {
  execFileSync(
    "docker",
    [
      "run",
      "--detach",
      "--name",
      containerName,
      "--read-only",
      "--cap-drop=ALL",
      "--security-opt",
      "no-new-privileges",
      "--memory=512m",
      "--cpus=0.5",
      "--tmpfs",
      "/tmp:rw,noexec,nosuid,size=64m",
      "-p",
      `${hostPort}:8080`,
      imageName
    ],
    { stdio: "inherit" }
  );
  containerStarted = true;

  await waitForHealth("startup");
  await waitForHealth("live");
  await waitForHealth("ready");
  await checkProtectedRoutes();

  const exitCode = stopContainer();
  console.log(`Container stopped cleanly with exit code ${exitCode}.`);
} catch (error) {
  printContainerLogs();
  throw error;
} finally {
  if (containerStarted) {
    spawnSync("docker", ["rm", "-f", containerName], { stdio: "ignore" });
  }
}

function checkImageContents() {
  const script = [
    "set -eu",
    "for path in /app/.git /app/.env /app/src /app/tests /app/TestResults /app/terraform.tfstate; do",
    "  if [ -e \"$path\" ]; then echo \"forbidden path present: $path\" >&2; exit 1; fi",
    "done",
    "if find /app \\( -name '*.cs' -o -name '*.csproj' -o -name '*.sln' -o -name '*.tfplan' -o -name '.env*' \\) -print -quit | grep -q .; then",
    "  echo 'source-only or local-only file present in final image' >&2",
    "  exit 1",
    "fi"
  ].join("\n");

  execFileSync("docker", ["run", "--rm", "--entrypoint", "/bin/sh", imageName, "-c", script], {
    stdio: "inherit"
  });
}

async function waitForHealth(name) {
  const url = `http://127.0.0.1:${hostPort}/health/${name}`;
  const deadline = Date.now() + 30_000;
  let lastError;

  while (Date.now() < deadline) {
    try {
      const response = await fetch(url);
      if (response.ok) {
        console.log(`${name} health passed.`);
        return;
      }

      lastError = new Error(`${url} returned ${response.status}`);
    } catch (error) {
      lastError = error;
    }

    await delay(500);
  }

  throw new Error(`${name} health did not pass: ${lastError?.message ?? "timed out"}`);
}

async function checkProtectedRoutes() {
  const checks = [
    {
      name: "planner read",
      method: "GET",
      path: "/api/v1/work-orders",
      token: tokens.plannerRead,
      expectedStatuses: [200, 503]
    },
    {
      name: "planner write",
      method: "POST",
      path: "/api/v1/planning-runs",
      token: tokens.plannerWrite,
      body: {
        idempotencyKey: `container-planning-${Date.now()}`,
        horizonStartUtc: "2026-01-15T00:00:00Z",
        horizonEndUtc: "2026-01-29T00:00:00Z",
        requestedBy: "container-smoke"
      },
      expectedStatuses: [202, 503]
    },
    {
      name: "imports",
      method: "POST",
      path: "/api/v1/imports/source-work-orders",
      token: tokens.imports,
      body: {
        sourceSystem: "synthetic-source",
        schemaVersion: "1.0",
        idempotencyKey: `container-smoke-${Date.now()}`,
        sourceWorkOrders: []
      },
      expectedStatuses: [200, 503]
    },
    {
      name: "operations",
      method: "GET",
      path: "/api/v1/operations/posture",
      token: tokens.operations,
      expectedStatuses: [200]
    }
  ];

  for (const check of checks) {
    await checkProtectedRoute(check);
  }
}

async function checkProtectedRoute({ name, method, path, token, body, expectedStatuses }) {
  const headers = { Authorization: `Bearer ${token}` };
  if (body) {
    headers["content-type"] = "application/json";
  }

  const response = await fetch(`http://127.0.0.1:${hostPort}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined
  });

  if (response.status === 401 || response.status === 403) {
    throw new Error(`${name} protected route returned ${response.status}; authorization did not pass.`);
  }

  if (!expectedStatuses.includes(response.status)) {
    const responseBody = await response.text();
    throw new Error(
      `${name} protected route returned ${response.status}; expected ${expectedStatuses.join(" or ")}. ${responseBody}`
    );
  }

  const persistenceNote =
    response.status === 503 ? " (accepted because persistence-backed route authorized before availability)" : "";
  console.log(`${name} protected route passed with ${response.status}${persistenceNote}.`);
}

function stopContainer() {
  execFileSync("docker", ["stop", "--timeout", "10", containerName], { stdio: "inherit" });

  const output = execFileSync("docker", ["inspect", "--format", "{{.State.ExitCode}}", containerName], {
    encoding: "utf8"
  }).trim();

  const exitCode = Number(output);
  if (!Number.isInteger(exitCode)) {
    throw new Error(`Could not read container exit code: ${output}`);
  }

  if (![0, 143].includes(exitCode)) {
    throw new Error(`Unexpected container exit code: ${exitCode}`);
  }

  return exitCode;
}

function printContainerLogs() {
  spawnSync("docker", ["logs", containerName], { stdio: "inherit" });
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
