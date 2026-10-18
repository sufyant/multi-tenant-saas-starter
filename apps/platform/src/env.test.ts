import { describe, expect, it } from "vitest";
import { readEnv } from "./env";

const valid = {
  API_URL: "https://api.example.test",
  NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY: "pk_test_x",
  CLERK_SECRET_KEY: "sk_test_x",
  NEXT_PUBLIC_CLERK_SIGN_IN_URL: "/sign-in",
};

describe("env", () => {
  it("ReadEnv_EveryVariable_ReturnsThem", () => {
    expect(readEnv(valid).API_URL).toBe("https://api.example.test");
  });

  it("ReadEnv_MissingApiUrl_FailsWithItsName", () => {
    expect(() => readEnv({ ...valid, API_URL: undefined })).toThrow("API_URL");
  });
});
