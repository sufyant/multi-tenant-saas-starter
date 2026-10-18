import { describe, expect, it } from "vitest";
import { z } from "zod";
import { zodErrorMap } from "./zod-error-map";

const invite = z.object({
  name: z.string().min(1).max(100),
  email: z.email(),
  role: z.enum(["owner", "admin", "member"]),
});

const keysOf = (input: unknown) =>
  invite
    .safeParse(input, { error: zodErrorMap })
    .error?.issues.map((issue) => [issue.path.join("."), issue.message]);

describe("zodErrorMap", () => {
  it("ZodErrorMap_TooLongAndBadEmailAndUnknownRole_GivesTheirMessageKeys", () => {
    expect(
      keysOf({ name: "a".repeat(101), email: "ali@", role: "guest" }),
    ).toEqual([
      ["name", "validation.too_long"],
      ["email", "validation.email_invalid"],
      ["role", "validation.choose_value"],
    ]);
  });

  it("ZodErrorMap_EmptyAndMissingFields_AreRequired", () => {
    expect(keysOf({ name: "", role: "member" })).toEqual([
      ["name", "validation.required"],
      ["email", "validation.required"],
    ]);
  });
});
