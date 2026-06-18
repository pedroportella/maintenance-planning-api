# Reviewer Runbook

Current state: initial .NET API and worker skeleton with health endpoints, OpenAPI JSON, safe errors and tests.

## Local Checks

```bash
dotnet format MaintenancePlanning.sln --verify-no-changes --no-restore
dotnet test MaintenancePlanning.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
node scripts/quality-guards.mjs all
node scripts/reviewer-evidence-smoke.mjs
```

## Future Local Smoke

The planned local smoke will:

1. wait for API readiness;
2. verify SQL Server readiness;
3. import a deterministic synthetic scenario;
4. retry the import and confirm idempotency;
5. start a planning run;
6. fetch recommendations;
7. fetch operations posture.
