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

// The rules of docs/frontend/ARCHITECTURE.md for apps/platform.
const src = import.meta.dirname;
const app = join(src, "..");
const generatedSchemas = join(
  app,
  "../../packages/api-client/src/generated/endpoints.schemas.gen.ts",
);
const languages = enumValues(generatedSchemas, "Language");

describe("architecture of apps/platform", () => {
  it("F17_AppsPlatform_UsesNoPhysicalDirectionClass", () => {
    expect(physicalDirectionClasses(src)).toEqual([]);
  });

  it("F21_AppsPlatform_UsesNoColourLiteral", () => {
    expect(colorLiterals(src)).toEqual([]);
  });

  it("F25_AppsPlatform_WritesNoUserFacingLiteral", () => {
    expect(userFacingLiterals(src)).toEqual([]);
  });

  it("F35_AppsPlatform_HasNoTestsFolder", () => {
    expect(testFolders(app)).toEqual([]);
  });

  it("F35_AppsPlatform_PutsEachTestBesideItsFile", () => {
    expect(testsBesideTheirFile(src)).toEqual([]);
  });

  it("F29_AppsPlatform_ImportsNoFeatureFromAnother", () => {
    expect(crossFeatureImports(src)).toEqual([]);
  });

  it("F15_AppsPlatformMessages_HaveTheSameKeysInEveryLanguage", () => {
    expect(messageKeySets(join(app, "messages"), languages)).toEqual([]);
  });

  it("F18_AppsPlatform_FormatsDatesOnlyInFormatDateTime", () => {
    expect(dateFormattingOutsideFormatter(src)).toEqual([]);
  });

  it("F51_AppsPlatform_DecidesNothingByARolesName", () => {
    expect(roleNameDecisions(src)).toEqual([]);
  });

  it("F3_AppsPlatform_WritesNoEnumValueAsALiteral", () => {
    expect(enumLiterals(src, contractEnumValues(generatedSchemas))).toEqual([]);
  });
});
