const imageDigestPattern = /@sha256:[a-f0-9]{64}$/;
const sensitiveEnvironmentNamePattern = /(password|secret|token|connectionstring|access[_-]?key|private[_-]?key)/i;

export function validateTaskDefinitionPayload(payload, options = {}) {
  const label = options.label ?? "task definition";
  const failures = [];

  if (!payload || typeof payload !== "object" || Array.isArray(payload)) {
    return [`${label} must be a JSON object.`];
  }

  if (!payload.family || typeof payload.family !== "string") {
    failures.push(`${label} must include a family.`);
  }

  if (payload.networkMode !== "awsvpc") {
    failures.push(`${label} must use awsvpc network mode.`);
  }

  if (!Array.isArray(payload.requiresCompatibilities) || !payload.requiresCompatibilities.includes("FARGATE")) {
    failures.push(`${label} must require FARGATE compatibility.`);
  }

  if (!Array.isArray(payload.containerDefinitions) || payload.containerDefinitions.length === 0) {
    failures.push(`${label} must include at least one container definition.`);
    return failures;
  }

  for (const container of payload.containerDefinitions) {
    const containerName = container.name ?? "<unnamed>";

    if (!container.image || typeof container.image !== "string") {
      failures.push(`${label} container ${containerName} must include an image.`);
    } else if (!imageDigestPattern.test(container.image)) {
      failures.push(`${label} container ${containerName} image must be pinned with @sha256:<digest>.`);
    }

    if (container.image?.includes(":latest")) {
      failures.push(`${label} container ${containerName} image must not use latest.`);
    }

    for (const entry of container.environment ?? []) {
      if (sensitiveEnvironmentNamePattern.test(entry.name ?? "") && typeof entry.value === "string") {
        failures.push(`${label} container ${containerName} must use an ECS secret for ${entry.name}.`);
      }
    }
  }

  if (options.requiredContainerName && !payload.containerDefinitions.some((entry) => entry.name === options.requiredContainerName)) {
    failures.push(`${label} must include container ${options.requiredContainerName}.`);
  }

  return failures;
}

export function evaluateRunTaskResult(response) {
  const failures = response?.failures ?? [];
  if (failures.length > 0) {
    return {
      ok: false,
      reason: failures
        .map((failure) => `${failure.arn ?? "unknown"} ${failure.reason ?? "unknown"} ${failure.detail ?? ""}`.trim())
        .join("; ")
    };
  }

  const taskArn = response?.tasks?.[0]?.taskArn;
  if (!taskArn) {
    return { ok: false, reason: "run-task returned no task ARN." };
  }

  return { ok: true, taskArn };
}

export function evaluateStoppedTask(response, containerName) {
  const task = response?.tasks?.[0];
  if (!task) {
    return { ok: false, reason: "describe-tasks returned no task." };
  }

  if (task.stopCode && task.stopCode !== "EssentialContainerExited") {
    return {
      ok: false,
      reason: `task stopped with ${task.stopCode}: ${task.stoppedReason ?? "no stopped reason"}`
    };
  }

  const container = task.containers?.find((candidate) => candidate.name === containerName);
  if (!container) {
    return { ok: false, reason: `container ${containerName} was not found in stopped task.` };
  }

  if (!Number.isInteger(container.exitCode)) {
    return {
      ok: false,
      reason: `container ${containerName} did not report an exit code: ${container.reason ?? "no container reason"}`
    };
  }

  if (container.exitCode !== 0) {
    return {
      ok: false,
      reason: `container ${containerName} exited with ${container.exitCode}: ${container.reason ?? "no container reason"}`
    };
  }

  return {
    ok: true,
    taskArn: task.taskArn,
    stoppedReason: task.stoppedReason ?? null,
    exitCode: container.exitCode
  };
}
