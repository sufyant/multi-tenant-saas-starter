import path from "node:path";
import { withSentryConfig } from "@sentry/nextjs/config";
import type { NextConfig } from "next";
import createNextIntlPlugin from "next-intl/plugin";

const nextConfig: NextConfig = {
  // A self-contained server for the container image (apps/web/Dockerfile). The repository root as the
  // tracing root brings the workspace packages into it. Vercel accepts both settings.
  output: "standalone",
  outputFileTracingRoot: path.join(__dirname, "../.."),
  transpilePackages: ["@repo/ui", "@repo/api-client"],
  // /dev/components is a `page.dev.tsx`: a page outside production builds only, so a production build has no such route (F33).
  pageExtensions:
    process.env.NODE_ENV === "production"
      ? ["ts", "tsx"]
      : ["ts", "tsx", "dev.tsx"],
  // Playwright's dev server sets E2E_DEV_SERVER (playwright.config.ts): Next.js's indicator sits at the foot of the rail and
  // covers the rail's toggle. The compile and runtime errors still show.
  ...(process.env.E2E_DEV_SERVER && { devIndicators: false }),
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "X-Frame-Options", value: "SAMEORIGIN" },
          {
            key: "Referrer-Policy",
            value: "strict-origin-when-cross-origin",
          },
          {
            key: "Permissions-Policy",
            value: "camera=(), microphone=(), geolocation=()",
          },
          {
            key: "Strict-Transport-Security",
            value: "max-age=63072000",
          },
        ],
      },
    ];
  },
};

// Error tracking (F55). The build gives each chunk a debug ID, and uploads the source maps only where CI gives the token
// (SENTRY_AUTH_TOKEN, with SENTRY_URL, SENTRY_ORG and SENTRY_PROJECT); then it deletes them, so the image serves none. Without
// the token the build uploads nothing and still deletes them. The release is the commit that CI passes as SOURCE_REVISION, the
// same in every environment; without it the events name no release. Errors only: no tracing code, no replay, no route list.
export default withSentryConfig(createNextIntlPlugin()(nextConfig), {
  telemetry: false,
  silent: !process.env.CI,
  release: { name: process.env.SOURCE_REVISION, create: false },
  buildTimeInstrumentation: false,
  routeManifestInjection: false,
  suppressOnRouterTransitionStartWarning: true,
  bundleSizeOptimizations: {
    excludeDebugStatements: true,
    excludeTracing: true,
    excludeReplayShadowDom: true,
    excludeReplayIframe: true,
    excludeReplayWorker: true,
  },
});
