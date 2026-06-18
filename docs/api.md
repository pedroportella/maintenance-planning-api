# API

Planned HTTP surface:

- `GET /health/startup`
- `GET /health/live`
- `GET /health/ready`
- `GET /openapi/v1.json`
- `GET /api/v1/operations/posture`
- `POST /api/v1/imports/source-work-orders`
- `POST /api/v1/imports/maintenance-events`
- `GET /api/v1/work-orders`
- `POST /api/v1/planning-runs`
- `GET /api/v1/planning-runs/{id}`
- `GET /api/v1/planning-runs/{id}/recommendations`
- `POST /api/v1/packages/{id}/decisions`

Errors should use `application/problem+json` with a correlation identifier.
