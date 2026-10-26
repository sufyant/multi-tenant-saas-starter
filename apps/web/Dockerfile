# syntax=docker/dockerfile:1

# The web app's image. CI builds, tests and publishes it once, and Railway runs that image by digest in
# every environment (docs/deployment.md). The build reads no setting that differs between environments: the server reads every
# setting at run time (F7). It reads only the release and the source map upload of error tracking (F55).
#
# Built from the repository root, because the build needs the lockfile and packages/ui:
#
#   docker build -f apps/web/Dockerfile -t web .

# Debian, like the runtime image, so the native packages the build installs match the runtime's C library.
# Its major version must agree with .nvmrc; the deps stage checks that.
FROM node:22.23.4-trixie-slim@sha256:8eb619e402eaede1748a3c8173bffab391592812e85136452f781741c7242ddf AS deps
# The Node major version from .nvmrc. CI passes it: --build-arg NODE_MAJOR="$(cat .nvmrc)".
ARG NODE_MAJOR=22
RUN test "$(node -p 'process.versions.node.split(".")[0]')" = "$NODE_MAJOR" \
    || { echo "The base image runs Node $(node -v), but NODE_MAJOR is $NODE_MAJOR. Update the FROM lines." >&2; exit 1; }
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
# packageManager in package.json fixes the pnpm version.
RUN corepack enable
WORKDIR /repo
# Manifests only, so a change to the source alone reuses the install layer.
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY apps/web/package.json apps/web/
COPY packages/ui/package.json packages/ui/
COPY packages/api-client/package.json packages/api-client/
RUN pnpm install --frozen-lockfile --filter "@repo/web..."

FROM deps AS build
# No telemetry: neither Next.js's nor that of the Sentry CLI, which uploads the source maps (F55).
ENV NEXT_TELEMETRY_DISABLED=1 SENTRY_CLI_NO_TELEMETRY=1
# The release that each error event names: the commit that CI builds, the same in every environment (F55). CI passes
# --build-arg SOURCE_REVISION="$GITHUB_SHA". Without it the build works and the events name no release.
ARG SOURCE_REVISION
# Where the build uploads its source maps: Better Stack's source map host, the team and the application (F55). None of them is
# secret; CI passes them from GitHub variables.
ARG SENTRY_URL SENTRY_ORG SENTRY_PROJECT
# .dockerignore keeps node_modules out of the context, so this COPY does not replace the installed tree.
COPY . .
# The upload's token is a BuildKit secret: it exists as SENTRY_AUTH_TOKEN for this RUN only and never reaches a layer, the image's
# history or a build argument (Docker docs, "Build secrets"). Without the secret (a local build, a fork's pull request) the build
# uploads nothing and does not fail. Either way the build deletes the source maps, so the image serves none.
# /next-cache is an empty cache directory for the runtime. The build's own .next/cache holds the build cache,
# which the runtime does not need.
RUN --mount=type=secret,id=sentry_auth_token,env=SENTRY_AUTH_TOKEN \
    pnpm --filter @repo/web build && mkdir /next-cache
# The Sentry CLI is FSL-1.1 licensed and allowed for the build's upload only (F38): the runtime stage copies these folders, so
# none of them may hold it.
RUN if find apps/web/.next/standalone apps/web/.next/static apps/web/public \
         \( -path '*/node_modules/sentry' -o -path '*/node_modules/@sentry/cli' \) -print | grep .; then \
      echo "The runtime image would hold the Sentry CLI. It may run at build time only (F38)." >&2; exit 1; \
    fi

# Distroless: Node and nothing else. No shell, no npm, no package manager.
FROM gcr.io/distroless/nodejs22-debian13:nonroot@sha256:668bb45274f097b5b8b41e9825a9bbd3b5e5c44cec5c50c61fe2a1b6ce5f6ba6 AS runtime
# The base image runs as its non-root user. Root creates /app, so the user cannot add, rename or delete files there.
USER 0:0
WORKDIR /app
ENV NODE_ENV=production NEXT_TELEMETRY_DISABLED=1 HOSTNAME=0.0.0.0 PORT=3000
# The standalone output holds server.js and the traced node_modules. outputFileTracingRoot is the
# repository root, so it keeps the workspace layout. The build does not copy public/ and .next/static/
# into it; without a CDN, the server serves them.
COPY --from=build /repo/apps/web/.next/standalone ./
COPY --from=build /repo/apps/web/.next/static ./apps/web/.next/static
COPY --from=build /repo/apps/web/public ./apps/web/public
# Next.js writes optimised images and revalidated pages to .next/cache at run time. The user may write there and nowhere else.
COPY --from=build --chown=65532:65532 /next-cache ./apps/web/.next/cache

# The base image's non-root user `nonroot` and its group (CIS Docker Benchmark 4.1). A number, so Kubernetes
# can verify runAsNonRoot (hadolint DL3066). The other files stay owned by root, so the process cannot change them.
USER 65532:65532
EXPOSE 3000
# The image's entrypoint is node.
CMD ["apps/web/server.js"]
