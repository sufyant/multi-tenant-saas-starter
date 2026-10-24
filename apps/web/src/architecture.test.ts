import { join } from "node:path";
import {
  colorLiterals,
  contractEnumValues,
  crossFeatureImports,
  dateFormattingOutsideFormatter,
  declarationsOf,
  enumLiterals,
  enumValues,
  messageKeySets,
  nativeChoiceControls,
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
// The parts both apps share from packages/ui since step 22k: an app never declares them again (F29).
const sharedParts = [
  "useFailureText",
  "ListError",
  "Search",
  "Avatar",
  "initialsOf",
  "groupByDay",
  "DayGroup",
];

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

  it("F3_AppsWeb_TakesTheBuiltInRoleNamesFromTheClient", () => {
    expect(declarationsOf(src, ["BuiltInRole", "builtInRoleOf"])).toEqual([]);
  });

  it("F3_AppsWeb_WritesNoEnumValueAsALiteral", () => {
    expect(enumLiterals(src, contractEnumValues(generatedSchemas))).toEqual([]);
  });

  it("F52_AppsWeb_UsesNoNativeCheckboxOrRadio", () => {
    expect(nativeChoiceControls(src)).toEqual([]);
  });

  it("F29_AppsWeb_DeclaresNoSecondCopyOfAContractHelper", () => {
    expect(
      declarationsOf(src, ["problemOf", "referenceOf"], { rule: "F29" }),
    ).toEqual([]);
  });

  it("F29_AppsWeb_DeclaresNoSecondCopyOfASharedPart", () => {
    expect(
      declarationsOf(src, sharedParts, { from: "@repo/ui", rule: "F29" }),
    ).toEqual([]);
  });
});
