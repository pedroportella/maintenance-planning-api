# Migration Release Gate

The release gate keeps schema changes out of API startup and out of Terraform execution. Terraform provisions the ECS task, roles, networking and secrets. Release orchestration builds the migration artefact, runs it as a private one-off task, verifies the task result, then updates the API service to the exact task-definition revision for the release.

## Local Artefact Build

Build the EF Core migration bundle without contacting a database:

```bash
node scripts/build-migration-bundle.mjs
```

The default output goes under `artifacts/`, which is local-only. The framework-dependent bundle uses the same migrations assembly as the API and reads database settings from runtime environment variables when it executes. Set `MIGRATION_BUNDLE_RUNTIME` only when a release pipeline needs to cross-target a specific runtime.

Build the migration-runner image:

```bash
node scripts/migration-container-build.mjs
```

The migration-runner image is separate from the API image. It contains only the migration bundle and runs as the non-root base-image user.

## Dry-Run Validation

Validate task-definition payloads, digest-pinned images, private networking inputs and migration task-result parsing without AWS credentials:

```bash
node scripts/ecs-release-gate-tests.mjs
npm run deploy:release-gate:dry-run
```

The example task definitions in [deploy/release](../deploy/release/) are synthetic dry-run fixtures. Real release task definitions should be rendered from the release pipeline or copied from reviewed ECS task-definition templates, then updated with the image digests for the same source revision.

## Release Order

For a review release:

1. Build and test the API.
2. Build the migration bundle and migration-runner image from the same source revision.
3. Push API and migration-runner images to ECR and record their immutable digests.
4. Render API and migration task-definition JSON using image references formatted as `repository-url@sha256:<digest>`.
5. Run the migration release gate:

```bash
node scripts/ecs-migration-release-gate.mjs \
  --cluster "$(terraform -chdir=infra/aws output -raw cluster_name)" \
  --service "$(terraform -chdir=infra/aws output -raw api_service_name)" \
  --subnets "<comma-separated-private-subnet-ids>" \
  --security-groups "$(terraform -chdir=infra/aws output -raw migration_security_group_id)" \
  --api-task-definition <api-task-definition.json> \
  --migration-task-definition <migration-task-definition.json>
```

Use `terraform -chdir=infra/aws output -json private_subnet_ids` to read the private subnet list, then convert it to a comma-separated value before invoking the command. Keep local backend, variable and secret values out of committed files.

The script:

- registers the migration and API task definitions from the supplied JSON;
- launches the migration task with `assignPublicIp=DISABLED`;
- fails if ECS reports launch failures or does not return a task ARN;
- waits for the migration task to stop;
- checks the named `migration-runner` container exit code, stop code and stopped reason;
- updates the API service only after the migration task succeeds;
- waits for the API service to become stable.

## Failure Handling

The release gate fails before the API service update when:

- `run-task` reports ECS failures;
- no task ARN is returned;
- the migration task fails to start;
- the migration container is missing;
- the migration container has no exit code;
- the migration container exits with a non-zero code.

After a failed migration, inspect CloudWatch logs and the ECS stopped reason. Do not re-run blindly if the migration may have partially completed. Prefer forward fixes and reviewed follow-up migrations.

## Compatibility Notes

Schema changes should be expand-contract compatible with currently running API tasks. Add nullable columns, additive tables and backward-compatible indexes before requiring new application behaviour. Remove old columns or tighten constraints only after all running tasks are known to be compatible.

The gate does not promise exactly-once execution or atomic rollout across database and service changes. It gives the release a clear stop point before the API service revision changes. Long backfills should be split from request-path schema changes and run through separate reviewed operations.
