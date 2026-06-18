ARG DOTNET_IMAGE_TAG=8.0-bookworm-slim

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_IMAGE_TAG} AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj src/MaintenancePlanning.Api/
COPY src/MaintenancePlanning.Application/MaintenancePlanning.Application.csproj src/MaintenancePlanning.Application/
COPY src/MaintenancePlanning.Domain/MaintenancePlanning.Domain.csproj src/MaintenancePlanning.Domain/
COPY src/MaintenancePlanning.Infrastructure/MaintenancePlanning.Infrastructure.csproj src/MaintenancePlanning.Infrastructure/

RUN dotnet restore src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj

COPY src/ src/

RUN dotnet publish src/MaintenancePlanning.Api/MaintenancePlanning.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_IMAGE_TAG} AS runtime
WORKDIR /app

ARG IMAGE_REPOSITORY=maintenance-planning-api
ARG VCS_REF=local

LABEL org.opencontainers.image.title="Maintenance Planning API" \
      org.opencontainers.image.description="Review API runtime image for synthetic maintenance-planning workflows." \
      org.opencontainers.image.revision="${VCS_REF}" \
      org.opencontainers.image.source="${IMAGE_REPOSITORY}"

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

COPY --from=build /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "MaintenancePlanning.Api.dll"]
