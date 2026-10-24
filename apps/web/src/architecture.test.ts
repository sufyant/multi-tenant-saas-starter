import { join } from "node:path";
import {
  colorLiterals,
  contractEnumValues,
  crossFeatureImports,
  dateFormattingOutsideFormatter,
  enumLiterals,
  enumValues,
  messageKeySets,
  physicalDirectionClasses,
  roleNameDecisions,
  testFolders,
  testsBesideTheirFile,
  userFacingLiterals,
} from "@repo/ui/test/architecture-rules";
import { describe, expect, it } from "vitest";

// The rules of docs/frontend/ARCHITECTURE.md for apps/web.
const src = import.meta.dirname;
const app = join(src, "..");
const generatedSchemas = join(
  app,
  "../../packages/api-client/src/generated/endpoints.schemas.gen.ts",
);
const languages = enumValues(generatedSchemas, "Language");

describe("architecture of apps/web", () => {
  it("F17_AppsWeb_UsesNoPhysicalDirectionClass", () => {
    expect(physicalDirectionClasses(src)).toEqual([]);
  });

  it("F21_AppsWeb_UsesNoColourLiteral", () => {
    expect(colorLiterals(src)).toEqual([]);
  });

  it("F25_AppsWeb_WritesNoUserFacingLiteral", () => {
    expect(userFacingLiterals(src)).toEqual([]);
  });

  it("F35_AppsWeb_HasNoTestsFolder", () => {
    expect(testFolders(app)).toEqual([]);
  });

  it("F35_AppsWeb_PutsEachTestBesideItsFile", () => {
    expect(testsBesideTheirFile(src)).toEqual([]);
  });

  it("F29_AppsWeb_ImportsNoFeatureFromAnother", () => {
    expect(crossFeatureImports(src)).toEqual([]);
  });

  it("F15_AppsWebMessages_HaveTheSameKeysInEveryLanguage", () => {
    expect(messageKeySets(join(app, "messages"), languages)).toEqual([]);
  });

  it("F18_AppsWeb_FormatsDatesOnlyInFormatDateTime", () => {
    expect(dateFormattingOutsideFormatter(src)).toEqual([]);
  });

  it("F51_AppsWeb_DecidesNothingByARolesName", () => {
    expect(
      roleNameDecisions(src, {
        ignore: ["shared/roles.ts", "shared/use-role-name.ts"],
      }),
    ).toEqual([]);
  });

  it("F3_AppsWeb_WritesNoEnumValueAsALiteral", () => {
    expect(enumLiterals(src, contractEnumValues(generatedSchemas))).toEqual([]);
  });
});
