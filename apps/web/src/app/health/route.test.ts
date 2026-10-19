import { existsSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import infrastructure from "../../../../../.railway/web.json";
import { GET } from "./route";

describe("health", () => {
  // Railway's health check and the deploy's smoke test read the path from .railway/web.json.
  it("Route_HealthPathOfTheInfrastructureFile_IsThisRoute", () => {
    const route = path.join(
      __dirname,
      "..",
      infrastructure.healthPath,
      "route.ts",
    );

    expect(existsSync(route)).toBe(true);
    expect(path.dirname(route)).toBe(__dirname);
  });

  it("GET_Always_Answers200WithNoBody", async () => {
    const response = GET();

    expect(response.status).toBe(200);
    expect(await response.text()).toBe("");
  });
});
