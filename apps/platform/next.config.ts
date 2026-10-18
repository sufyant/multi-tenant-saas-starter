import path from "node:path";
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

export default createNextIntlPlugin()(nextConfig);
