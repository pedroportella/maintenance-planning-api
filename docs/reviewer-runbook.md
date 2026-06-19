# Reviewer Runbook

Current state: .NET API and worker skeleton with health endpoints, OpenAPI JSON, safe errors, tests, SQL Server persistence through EF Core migrations, local HTTP import contracts for synthetic source-system-shaped events, planner work-order query routes, local review auth policies, command rate limiting and a containerised API runtime path.

## Local Checks

```bash
dotnet format MaintenancePlanning.sln --verify-no-changes --no-restore
dotnet test MaintenancePlanning.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
node scripts/quality-guards.mjs all
node scripts/reviewer-evidence-smoke.mjs
node scripts/container-smoke.mjs
node scripts/database-smoke.mjs
```

## Database Smoke

```bash
node scripts/database-smoke.mjs
```

The smoke starts an isolated local SQL Server compose project, applies EF Core migrations explicitly, starts the API with database configuration, checks `/health/ready`, checks protected `/api/v1/operations/migration-readiness` with the synthetic reviewer token, then removes the isolated compose resources.

## Container Smoke

```bash
node scripts/container-build.mjs
node scripts/container-smoke.mjs --skip-build
```

The smoke checks startup, liveness, readiness, final image contents and graceful container stop behaviour.

## Future Local Smoke

The planned local smoke will:

1. wait for API readiness;
2. verify SQL Server readiness;
3. post a deterministic synthetic scenario to the local import endpoint;
4. retry the import and confirm idempotency;
5. start a planning run;
6. fetch work-order backlog and recommendations;
7. fetch operations posture.
