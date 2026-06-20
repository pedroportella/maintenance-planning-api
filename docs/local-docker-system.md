# Local Docker System Runbook

This runbook starts the local synthetic review system with Docker:

- SQL Server for local persistence;
- the API container;
- the simulator container for seeding and sanity checks;
- the web container in API-backed mode.

Run the commands from a parent directory that contains these sibling repositories:

```text
maintenance-planning-api
maintenance-data-simulator
maintenance-planning-web
```

All command blocks below assume your shell is in that parent directory.

## Ports

| Service | Local URL or Port |
| --- | --- |
| SQL Server | `127.0.0.1:14333` |
| API | `http://127.0.0.1:5000` |
| Web | `http://127.0.0.1:3104` |

## Build Images

```bash
npm --prefix maintenance-planning-api run container:build
npm --prefix maintenance-planning-api run migration:container:build
```

```bash
npm --prefix maintenance-data-simulator run container:build
```

```bash
pnpm --dir maintenance-planning-web container:build
```

The migration-runner image is a short-lived helper that applies EF Core migrations to the local SQL Server database. The API image does not apply migrations on startup.

## Start SQL Server

```bash
cp maintenance-planning-api/.env.local.example maintenance-planning-api/.env.local
docker compose \
  --env-file maintenance-planning-api/.env.local \
  -f maintenance-planning-api/docker-compose.yml \
  --profile sqlserver \
  up -d sqlserver
```

Wait until SQL Server is accepting connections. If the next migration command fails immediately with a login or connection error, wait a few more seconds and retry it.

## Apply Migrations

Use `host.docker.internal` because the migration-runner container connects back to SQL Server through the host-published port:

```bash
docker run --rm \
  --env-file maintenance-planning-api/.env.local \
  -e MaintenancePlanning__Database__Server=host.docker.internal,14333 \
  maintenance-planning-migration-runner:local
```

On Linux, add this flag to Docker commands that use `host.docker.internal`:

```text
--add-host=host.docker.internal:host-gateway
```

## Run The API

Keep this running in its own terminal:

```bash
docker run --rm \
  --name maintenance-planning-api-local \
  --env-file maintenance-planning-api/.env.local \
  -e MaintenancePlanning__Database__Server=host.docker.internal,14333 \
  -p 5000:8080 \
  maintenance-planning-api:local
```

Useful checks from another terminal:

```bash
curl -fsS http://127.0.0.1:5000/health/live
curl -fsS http://127.0.0.1:5000/health/ready
curl -fsS http://127.0.0.1:5000/openapi/v1.json
```

Protected `/api/v1` routes use synthetic local bearer tokens. For a broad local sanity check, use `local-reviewer-token`.

## Seed And Sanity-Check With The Simulator

Run this after the API is healthy:

```bash
docker run --rm \
  maintenance-data-simulator:local \
  api-smoke \
  --scenario baseline-week \
  --api-url http://host.docker.internal:5000 \
  --api-token local-reviewer-token
```

This waits for API readiness, posts deterministic synthetic maintenance events, replays the same import to check idempotency, creates a planning run, checks recommendations, records a synthetic package decision and reads operations posture.

For a lighter seed-only command:

```bash
docker run --rm \
  maintenance-data-simulator:local \
  feed \
  --scenario baseline-week \
  --api-url http://host.docker.internal:5000 \
  --api-token local-reviewer-token
```

## Run The Web Container

After the simulator smoke has loaded the baseline scenario, start the web container in API-backed mode:

```bash
MAINTENANCE_PLANNING_WEB_DATA_MODE=backend \
MAINTENANCE_PLANNING_API_URL=http://host.docker.internal:5000 \
MAINTENANCE_PLANNING_API_TOKEN=local-reviewer-token \
MAINTENANCE_PLANNING_WEB_BACKEND_HORIZON_START_UTC=2026-01-16T00:00:00Z \
MAINTENANCE_PLANNING_WEB_BACKEND_HORIZON_END_UTC=2026-01-30T00:00:00Z \
MAINTENANCE_PLANNING_WEB_BACKEND_REQUESTED_BY=local-docker-review \
pnpm --dir maintenance-planning-web container:run
```

Open `http://127.0.0.1:3104`.

Useful web checks:

```bash
curl -fsS http://127.0.0.1:3104/health/live
```

Planner routes worth checking in the browser:

- `http://127.0.0.1:3104/recommendations`
- `http://127.0.0.1:3104/operations-posture`
- `http://127.0.0.1:3104/scenario-outcomes`

The API URL and token are server-side web runtime settings. Do not expose them through `NEXT_PUBLIC_*` variables.

## Mock Web Mode

If you only want to check the web image without the API, run:

```bash
pnpm --dir maintenance-planning-web container:run
```

That starts deterministic mock mode on `http://127.0.0.1:3104`.

## Stop Everything

Stop the API and web terminals with `Ctrl+C`, then stop SQL Server:

```bash
docker compose \
  --env-file maintenance-planning-api/.env.local \
  -f maintenance-planning-api/docker-compose.yml \
  --profile sqlserver \
  down
```

## What This Proves

This local path proves that:

- the images build locally;
- the API starts against SQL Server with explicit migrations;
- the simulator can feed deterministic synthetic data into the API;
- import idempotency, planning recommendations, package decisions and operations posture work through the HTTP boundary;
- the web image can render planner routes from the server-side API adapter.

This local path does not prove AWS EventBridge, SQS, worker ingestion, registry digest promotion, Terraform deployment or live review endpoint behaviour. Keep those as separate review checks.
