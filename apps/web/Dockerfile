# syntax=docker/dockerfile:1

# The web app outside Vercel. Vercel builds the app from source, so this image is not deployed.
# CI builds and runs it to prove that the app does not depend on one host.
#
# Built from the repository root, because the build needs the lockfile and packages/ui:
#
#   docker build -f apps/web/Dockerfile -t web .

# Debian, like the runtime image, so the native packages the build installs match the runtime's C library.
# Its major version must agree with .nvmrc; the deps stage checks that.
FROM node:22.23.3-trixie-slim@sha256:154ba2f4d6fec323d28e4f4bb86bba4677f1223391a1979cf521304e03a98dfa AS deps
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
ENV NEXT_TELEMETRY_DISABLED=1
# .dockerignore keeps node_modules out of the context, so this COPY does not replace the installed tree.
COPY . .
# /next-cache is an empty cache directory for the runtime. The build's own .next/cache holds the build cache,
# which the runtime does not need.
RUN pnpm --filter @repo/web build && mkdir /next-cache

# Distroless: Node and nothing else. No shell, no npm, no package manager.
FROM gcr.io/distroless/nodejs22-debian13:nonroot@sha256:ec2313763dd43931543bd03830466e0c409ce73a487e8d46f10db72d3b816c1c AS runtime
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
