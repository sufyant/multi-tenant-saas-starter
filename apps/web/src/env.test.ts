import { describe, expect, it } from "vitest";
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
