import { readFileSync } from "node:fs";
import path from "node:path";
import { createRailwayContext, project } from "railway/iac";
import { describe, expect, it } from "vitest";
import api from "../../../.railway/api.json";
import program from "../../../.railway/railway";
import infrastructure from "../../../.railway/web.json";
import { readEnv } from "./env";

const valid = {
  API_URL: "https://api.example.test",
  CLERK_PUBLISHABLE_KEY: "pk_test_x",
  CLERK_SECRET_KEY: "sk_test_x",
};

describe("env", () => {
  it("ReadEnv_EveryVariable_ReturnsThem", () => {
    expect(readEnv(valid).API_URL).toBe("https://api.example.test");
  });

  it("ReadEnv_MissingApiUrl_FailsWithItsName", () => {
    expect(() => readEnv({ ...valid, API_URL: undefined })).toThrow("API_URL");
  });

  // Decision 1 of the deploy step: the image reads the publishable key at run time, so its name has no NEXT_PUBLIC_ (F7).
  it("ReadEnv_MissingPublishableKey_FailsWithItsName", () => {
    expect(() =>
      readEnv({
        ...valid,
        CLERK_PUBLISHABLE_KEY: undefined,
        NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY: "pk_test_x",
      }),
    ).toThrow("CLERK_PUBLISHABLE_KEY");
  });
});

// .railway/railway.ts gives the web service what the image needs to start (F7, F46). The test evaluates the file with the
// SDK's own context and reads the service that it declares.
const graph = await program(
  createRailwayContext({ projectName: "project" }),
  project,
);
const service = graph.resources
  ?.flat()
  .find(
    (resource) =>
      "name" in resource && resource.name === infrastructure.service,
  ) as unknown as {
  source?: unknown;
  deploy: { healthcheckPath: string };
  variables: Record<string, unknown>;
};

const exampleKeys = readFileSync(
  path.join(__dirname, "..", ".env.example"),
  "utf8",
)
  .split("\n")
  .filter((line) => /^[A-Z_]+=/.test(line))
  .map((line) => line.slice(0, line.indexOf("=")));

const literal = (value: string) => ({ type: "literal", value });
const shared = (name: string) => ({ type: "sharedReference", name });

describe("env in railway.ts", () => {
  it("DefineService_TheApp_SetsEveryVariableOfEnvExampleAndThePort", () => {
    expect(Object.keys(service.variables).sort()).toEqual(
      [...exampleKeys, "PORT"].sort(),
    );
    expect(service.variables.PORT).toEqual(
      literal(String(infrastructure.port)),
    );
  });

  it("DefineService_TheApp_ChecksTheHealthPathOfItsFile", () => {
    expect(service.deploy.healthcheckPath).toBe(infrastructure.healthPath);
  });

  // Decision 2 of the deploy step: the API over the private network, as Railway's reference to api-web's private domain.
  it("DefineService_TheApp_ReachesTheApiOverThePrivateNetwork", () => {
    expect(service.variables.API_URL).toEqual(
      literal(
        `http://\${{${api.services.web}.RAILWAY_PRIVATE_DOMAIN}}:${api.publicPort}`,
      ),
    );
  });

  // The Clerk secret key has one source: the shared variable that the API reads too.
  it("DefineService_TheApp_ReadsTheClerkKeysFromSharedVariables", () => {
    expect(service.variables.CLERK_SECRET_KEY).toEqual(
      shared("ControlPlane__Clerk__SecretKey"),
    );
    expect(service.variables.CLERK_PUBLISHABLE_KEY).toEqual(
      shared("CLERK_PUBLISHABLE_KEY"),
    );
  });

  // CI sets the image by digest, as for the API.
  it("DefineService_TheApp_HasNoSource", () => {
    expect(service.source).toBeUndefined();
  });
});
