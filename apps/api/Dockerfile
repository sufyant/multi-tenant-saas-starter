# syntax=docker/dockerfile:1

# One image for every API process (section 1 of docs/ARCHITECTURE.md).
# The role comes from Host__Role; the migration is `docker run <image> migrate`.
#
#   docker build -t api apps/api

# 10.0.401 when pinned. Dependabot updates the digest, not this note.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /src

# What restore needs comes first, so a change to the source alone reuses the restore layer.
# COPY --parents keeps each project file in its own directory. .dockerignore leaves the test projects out.
COPY --parents global.json Directory.Build.props Directory.Packages.props .editorconfig src/**/*.csproj ./
RUN dotnet restore src/Api/Api.csproj

COPY src/ src/

# The OpenAPI document is committed and not part of the image, so the publish does not write it.
RUN dotnet publish src/Api/Api.csproj --configuration Release --no-restore --output /app \
    -p:UseAppHost=false -p:OpenApiGenerateDocuments=false

# Chiseled: no shell, no package manager. It runs in invariant globalization mode, and the code needs no ICU or time zone data.
# 10.0.12 when pinned. Dependabot updates the digest, not this note.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:48e51f2f6798897be7ac4e775c049ed8fe60d3190f637e1f9c9dc7513efa659c
# The base image runs as its non-root user. Root creates /app, so the user cannot add, rename or delete files there.
USER 0:0
WORKDIR /app
COPY --from=build /app .

# The base image's non-root user `app` (CIS Docker Benchmark 4.1).
# The build resolves APP_UID to the number 1654, which CI checks. hadolint cannot read the variable.
# hadolint ignore=DL3066
USER $APP_UID

# The base image sets ASPNETCORE_HTTP_PORTS=8080: the process listens on port 8080 on all interfaces.
EXPOSE 8080
# The management port (Host:ManagementPort): the health endpoints answer only there. Route no public traffic to it.
EXPOSE 8081

# No HEALTHCHECK: the image has no shell and no curl. The platform checks /health/live and /health/ready over HTTP.
ENTRYPOINT ["dotnet", "Api.dll"]
