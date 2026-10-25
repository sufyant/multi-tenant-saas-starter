# syntax=docker/dockerfile:1

# One image for every API process (section 1 of docs/ARCHITECTURE.md).
# The role comes from Host__Role; the migration is `docker run <image> migrate`.
#
#   docker build -t api --build-arg SOURCE_REVISION="$(git rev-parse HEAD)" apps/api

# 10.0.402 when pinned. Dependabot updates the digest, not this note.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS build
WORKDIR /src

# What restore needs comes first, so a change to the source alone reuses the restore layer.
# COPY --parents keeps each project file in its own directory. .dockerignore leaves the test projects out.
COPY --parents global.json Directory.Build.props Directory.Packages.props .editorconfig src/**/*.csproj ./
RUN dotnet restore src/Api/Api.csproj

COPY src/ src/

# The OpenAPI document is committed and not part of the image, so the publish does not write it.
RUN dotnet publish src/Api/Api.csproj --configuration Release --no-restore --output /app \
    -p:UseAppHost=false -p:OpenApiGenerateDocuments=false

# Chiseled: no shell, no package manager. It runs in invariant globalization mode, and the code needs no ICU.
# 10.0.13 when pinned. Dependabot updates the digest, not this note.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:5165aeeb688df15e869a56a7584bb94bf16a14e0b3ba77b49fcdbe350f6b8020
# The base image runs as its non-root user. Root creates /app, so the user cannot add, rename or delete files there.
USER 0:0
WORKDIR /app
# The time zone data, which the purge warning reads to write the moment in the Owner's zone (section 7). Only the data, not ICU:
# TimeZoneInfo reads the zone files, and the image keeps invariant globalization. The pinned SDK digest is Ubuntu 24.04, as the runtime
# is (the tag names no release), so a new digest of it brings the release's latest data.
COPY --from=build /usr/share/zoneinfo /usr/share/zoneinfo
COPY --from=build /app .

# The base image's non-root user `app` (CIS Docker Benchmark 4.1).
# The build resolves APP_UID to the number 1654, which CI checks. hadolint cannot read the variable.
# hadolint ignore=DL3066
USER $APP_UID

# The base image sets ASPNETCORE_HTTP_PORTS=8080: the process listens on port 8080 on all interfaces.
EXPOSE 8080
# The management port (Host:ManagementPort): the health endpoints answer only there. Route no public traffic to it.
EXPOSE 8081

# The commit the image is built from. CI passes it, so the image it promotes to production keeps it. Each error event
# names it as its release (Host:ErrorTracking:Release). A build without it leaves the release empty.
ARG SOURCE_REVISION=""
ENV Host__ErrorTracking__Release=$SOURCE_REVISION

# No HEALTHCHECK: the image has no shell and no curl. The platform checks /health/live and /health/ready over HTTP.
ENTRYPOINT ["dotnet", "Api.dll"]
