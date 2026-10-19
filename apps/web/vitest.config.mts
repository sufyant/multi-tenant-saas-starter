import react from "@vitejs/plugin-react";
import tsconfigPaths from "vite-tsconfig-paths";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [tsconfigPaths(), react()],
  test: {
    environment: "jsdom",
    setupFiles: ["./test/setup.ts"],
    // CI runs the builds beside the tests, so a test there takes up to 17 times as long as on a laptop. The slowest test of
    // this package took 5.7 s on CI and 0.6 s here (the table in pull request #103). 15 s leaves room; no test retries.
    testTimeout: 15_000,
    exclude: ["**/node_modules/**", "e2e/**", ".next/**"],
  },
});
