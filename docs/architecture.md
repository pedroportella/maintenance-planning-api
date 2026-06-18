# Architecture

The target architecture is a production-shaped .NET API and worker backed by SQL Server.

Planned flow:

```text
synthetic source events -> API import or EventBridge/SQS -> .NET worker/API -> SQL Server -> planning recommendations -> operations posture
```

This repository owns the API, worker, persistence, infrastructure and reviewer evidence. The source-system simulator lives in a separate repository.

## Boundary

All source data is synthetic. Real source-system access, employer/client systems, production identity, production resilience and formal assurance are production-next concerns.
