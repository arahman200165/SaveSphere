# syntax=docker/dockerfile:1
# Multi-stage build for the API and the explicit migrator (ADR-0004).
# Build context is the repository root:  docker build -f infrastructure/docker/Dockerfile --target api .
#
# The SDK image must stay in the same feature band as global.json (ADR-0001); the runtime image is the
# runtime that ships with that SDK. Bump both together.

ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.202
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.6

# ---- build ---------------------------------------------------------------------------------------
FROM ${SDK_IMAGE} AS build
WORKDIR /src

# Restore first, with only the files that affect it, so source edits do not invalidate the package layer.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY apps/backend/SaveSphere.SharedKernel/SaveSphere.SharedKernel.csproj apps/backend/SaveSphere.SharedKernel/
COPY apps/backend/SaveSphere.Infrastructure/SaveSphere.Infrastructure.csproj apps/backend/SaveSphere.Infrastructure/
COPY apps/backend/Modules/Core/SaveSphere.Modules.Core.csproj apps/backend/Modules/Core/
COPY apps/backend/SaveSphere.Api/SaveSphere.Api.csproj apps/backend/SaveSphere.Api/
COPY apps/backend/SaveSphere.Migrator/SaveSphere.Migrator.csproj apps/backend/SaveSphere.Migrator/
RUN dotnet restore apps/backend/SaveSphere.Api/SaveSphere.Api.csproj \
 && dotnet restore apps/backend/SaveSphere.Migrator/SaveSphere.Migrator.csproj

COPY apps apps
RUN dotnet publish apps/backend/SaveSphere.Api/SaveSphere.Api.csproj -c Release -o /out/api --no-restore \
 && dotnet publish apps/backend/SaveSphere.Migrator/SaveSphere.Migrator.csproj -c Release -o /out/migrator --no-restore

# ---- api -----------------------------------------------------------------------------------------
FROM ${RUNTIME_IMAGE} AS api
WORKDIR /app
COPY --from=build /out/api ./
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SaveSphere.Api.dll"]

# ---- migrator ------------------------------------------------------------------------------------
FROM ${RUNTIME_IMAGE} AS migrator
WORKDIR /app
COPY --from=build /out/migrator ./
ENV DOTNET_EnableDiagnostics=0
USER $APP_UID
ENTRYPOINT ["dotnet", "SaveSphere.Migrator.dll"]
