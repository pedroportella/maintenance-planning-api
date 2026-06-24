# Production-Next

Production work would require:

- real identity and authorisation;
- real source-system connectivity;
- private networking and stricter environment separation;
- resilience design and restore drills;
- full observability and alert routing;
- separate planner read/write authorization policies where production roles require that split;
- retry idempotency for planning-run creation and stricter outbox/replay audit semantics;
- stale-import and outbound outbox counts in operations posture;
- SBOM and provenance attestations, image signing and registry vulnerability scanning;
- threat modelling and independent security review;
- incident, ownership and cost-management processes.

The review environment should demonstrate the shape without claiming those controls are complete.
