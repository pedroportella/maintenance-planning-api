# Maintenance Planning API Agent Guide

Durable repo guidance for the maintenance-planning API prototype.

## Where Context Belongs

- Keep stable engineering rules here.
- Keep private research, stage notes and caveats in the parent workspace `ai-notes/`.
- Keep public README/docs short and neutral.
- Do not copy private planning history into public docs, UI copy or commit subjects.

## Project Boundary

- This is a neutral review prototype for maintenance planning and asset operations workflows.
- Use synthetic data only.
- Do not use company branding, industry-specific language, real client data, real source-system access or production-infrastructure claims.
- Prefer honest language: prototype, production-shaped, review environment, synthetic source, future integration point and production-next.

## Backend Rules

- Build a production-shaped .NET API and worker, not a thin mock API.
- Keep validation, idempotency, ingestion state, planning rules and persistence backend-authoritative.
- Keep transport thin; put rules in application/domain services and data access in infrastructure.
- Return safe problem-details errors with correlation IDs.
- Never expose stack traces, secrets, personal data or infrastructure details.
- Use OpenAPI for HTTP contracts and document event contracts separately.

## Integration Rules

- Treat source-system-shaped data as synthetic and adapter-ready.
- Preserve upstream source identifiers separately from product-owned state.
- Use idempotency keys, event ids, correlation ids and integration audit records.
- Surface source freshness, failed batches, queue depth and DLQ state in operations posture.

## AWS And Terraform Rules

- Terraform must be deployable but cost-controlled.
- Use remote state, tagged resources, Secrets Manager, private database subnets and documented teardown.
- Do not commit state, plans, secrets, account ids or generated sensitive outputs.
- Deployed resources belong to Pedro's review environment only.

## Tests And Checks

- Add focused tests when behaviour changes.
- Preserve local, deployed and event-path smoke checks.
- Run relevant guards after public docs, config, Terraform or contract changes.

## Documentation

- README should answer: what this is, how to run, what is real, what is synthetic and what production-next requires.
- Public docs are evidence notes, not implementation diaries.
- Refresh generated OpenAPI/AsyncAPI artefacts only when contract changes are intentional.
