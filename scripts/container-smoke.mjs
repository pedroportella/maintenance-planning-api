#!/usr/bin/env node

import { execFileSync, spawnSync } from "node:child_process";
import { createServer } from "node:net";

const imageName = process.env.IMAGE_NAME ?? "maintenance-planning-api:local";
const shouldBuild = !process.argv.includes("--skip-build");
const hostPort = process.env.HOST_PORT ?? String(await findFreePort());
const containerName =
  process.env.CONTAINER_NAME ?? `maintenance-planning-api-smoke-${Date.now()}`;

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
