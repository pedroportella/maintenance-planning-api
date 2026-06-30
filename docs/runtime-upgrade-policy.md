# Runtime Upgrade Policy

This repository keeps runtime changes deliberate because the API, worker, migration bundle, Docker images, EF tools and release-gate scripts all need to move together.

## Current Baseline

| Surface | Current setting |
| --- | --- |
| Project target framework | `net8.0` in `Directory.Build.props` |
| .NET SDK | `8.0.128` in `global.json` |
| SDK roll-forward | `latestPatch` in `global.json` |
| EF Core tool | `dotnet-ef` `8.0.28` in `.config/dotnet-tools.json` |
| API, worker and migration-runner images | `DOTNET_IMAGE_TAG=8.0-bookworm-slim` |

## Upgrade Gate

Do not change the target framework, SDK roll-forward, EF tools or Docker base image as part of reviewer-navigation cleanup. A runtime upgrade should have its own small stage with a verification plan that covers:

1. SDK installation and `global.json` policy.
2. Project target framework and NuGet package compatibility.
3. EF tooling and migration bundle generation.
4. API, worker and migration-runner Docker base images.
5. CI and local reviewer commands.
6. Unit and API tests.
7. Container smoke, worker image build and database smoke.
8. Migration release-gate dry run.

## Reviewer Evidence

Runtime policy evidence is local until an environment runs the upgraded images. Record the exact SDK version, image tags and command results in reviewer notes. Do not describe a runtime as adopted until the repository target framework, SDK policy, EF tools, Docker images and release-gate verification have all moved together.
