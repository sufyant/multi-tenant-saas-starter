import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    setupFiles: ["./test/setup.ts"],
    // CI runs the builds beside the tests, so a test there takes up to 17 times as long as on a laptop. The slowest test of
    // this package took 5.2 s on CI and 0.6 s here (the table in pull request #103). 15 s leaves room; no test retries.
    testTimeout: 15_000,
  },
});
