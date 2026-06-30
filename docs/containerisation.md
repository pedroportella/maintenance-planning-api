# Containerisation

The API image is the runtime boundary for local review, CI and later cloud deployment work. It uses a conventional multi-stage Dockerfile so the final image contains the published API output only, not the SDK, repository metadata, test projects, source files or local configuration.

For the cross-repo local Docker recipe, including API, simulator and web commands, see the [local Docker system runbook](local-docker-system.md).

## Local Image Build

```bash
docker build -t maintenance-planning-api:local .
```

Equivalent script:

```bash
node scripts/container-build.mjs
```

Set `IMAGE_NAME`, `IMAGE_REPOSITORY` or `SOURCE_REVISION` to override the default local tag and image metadata.

## Local Container Run

```bash
docker run --rm -p 5000:8080 maintenance-planning-api:local
```

Equivalent script:

```bash
node scripts/container-run.mjs
```

Useful health checks:

```bash
curl -fsS http://localhost:5000/health/startup
curl -fsS http://localhost:5000/health/live
curl -fsS http://localhost:5000/health/ready
```

## Local SQL Server

SQL Server can be started for local persistence checks through the compose profile:

```bash
cp .env.local.example .env.local
docker compose --env-file .env.local --profile sqlserver up -d sqlserver
```

The default host port is `14333`. The API uses explicit environment variables for local database configuration; copy `.env.local.example` to `.env.local` for local values.

Apply migrations explicitly before expecting database-backed readiness to pass:

```bash
dotnet dotnet-ef database update --project src/MaintenancePlanning.Infrastructure/MaintenancePlanning.Infrastructure.csproj --startup-project src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj --context MaintenancePlanningDbContext
```

## Restricted Smoke

The smoke script builds the image unless `--skip-build` is supplied, checks that source-only and local-only files are absent from the final image, starts the API with runtime restrictions, waits for startup, liveness and readiness health, then stops the container.

```bash
node scripts/container-smoke.mjs
```

The restricted run uses a non-root image user, a read-only root filesystem, dropped Linux capabilities, no-new-privileges, a temporary `/tmp` mount, and small CPU and memory limits:

```bash
docker run --rm --read-only --cap-drop=ALL --security-opt no-new-privileges --memory=512m --cpus=0.5 --tmpfs /tmp:rw,noexec,nosuid,size=64m -p 5000:8080 maintenance-planning-api:local
```

## Base Image Rationale

The build stage uses the maintained Microsoft .NET SDK image and the runtime stage uses the matching ASP.NET Core runtime image. The runtime image listens on container port `8080` and uses the built-in non-root app user from the base image.

The image tag is pinned to the .NET major line and Debian slim variant. Base-image updates should be reviewed through normal dependency maintenance and verified by the container smoke.

## Image Identity

Local development uses explicit local tags such as `maintenance-planning-api:local`.

CI and later release flows should use the source revision as an image tag and as OCI image metadata for traceability. Deployment should select an immutable image digest rather than relying on a mutable tag. Registry tag immutability is a useful supporting control, but the deployment identity should still be the digest.

Avoid relying on an implicit `latest` tag.

## Migration Runner Image

The migration runner has a separate Dockerfile, [Dockerfile.migrations](../Dockerfile.migrations), and a separate build script:

```bash
node scripts/migration-container-build.mjs
```

The image contains an EF Core migration bundle and no API listener. It is intended for one-off ECS task execution through the [migration release gate](release-gate.md), not for API startup migrations.

## Worker Image

The event ingestion worker has a separate Dockerfile, [Dockerfile.worker](../Dockerfile.worker), and build script:

```bash
node scripts/worker-container-build.mjs
```

The worker image runs `MaintenancePlanning.Worker.dll`, polls the configured SQS work queue, processes EventBridge-delivered synthetic maintenance events through the same import contract as the API, deletes only messages that were processed or safely audited as invalid, and dispatches pending outbound outbox events to EventBridge when an event bus is configured. Runtime queue identifiers, event bus name and database passwords are task-definition configuration and Secrets Manager values, not image build inputs. Local worker image builds prove the runtime can be packaged; EventBridge/SQS consumption and outbound publish evidence still require the separate live review smoke.

## Secrets And Build Inputs

The current image build does not require registry, package-feed or cloud secrets. If private feeds are introduced later, use BuildKit secret or SSH mounts. Do not pass build secrets through Dockerfile arguments or environment variables.

## Writable Paths

The API does not require application-owned writable storage in the container. A temporary `/tmp` mount is provided during restricted smoke runs for runtime libraries that expect a temporary location.

## Production-Next

Production release work should add SBOM and provenance attestations, image signing and verification, registry vulnerability scanning, digest-based deployment in task definitions, and reviewed policies for rebuilding images when maintained base images are updated.
