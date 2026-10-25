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
  numericLayerClasses,
  physicalDirectionClasses,
  roleNameDecisions,
  sdkImportRefusals,
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

describe("architecture of apps/platform", () => {
  it("F17_AppsPlatform_UsesNoPhysicalDirectionClass", () => {
    expect(physicalDirectionClasses(src)).toEqual([]);
  });

  it("F21_AppsPlatform_UsesNoColourLiteral", () => {
    expect(colorLiterals(src)).toEqual([]);
  });

  it("F53_AppsPlatform_UsesNoNumericLayerClass", () => {
    expect(numericLayerClasses(src)).toEqual([]);
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
    // A role's text, and Make Owner disabled on an Owner membership: the platform's one exception (F3).
    expect(
      roleNameDecisions(src, {
        ignore: [
          "features/users/use-role-name.ts",
          "features/users/owner-membership.ts",
        ],
      }),
    ).toEqual([]);
  });

  it("F3_AppsPlatform_WritesNoEnumValueAsALiteral", () => {
    expect(enumLiterals(src, contractEnumValues(generatedSchemas))).toEqual([]);
  });

  it("F52_AppsPlatform_UsesNoNativeCheckboxOrRadio", () => {
    expect(nativeChoiceControls(src)).toEqual([]);
  });

  it("F29_AppsPlatform_DeclaresNoSecondCopyOfAContractHelper", () => {
    expect(
      declarationsOf(src, ["problemOf", "referenceOf"], { rule: "F29" }),
    ).toEqual([]);
  });

  it("F29_AppsPlatform_DeclaresNoSecondCopyOfASharedPart", () => {
    expect(
      declarationsOf(src, sharedParts, { from: "@repo/ui", rule: "F29" }),
    ).toEqual([]);
  });
  // Only the app's error tracking folder and next.config.ts import the SDK; a feature reports through reportError (F55).
  it("F55_AppsPlatform_ImportsTheErrorTrackerOnlyInItsConfig", () => {
    expect(
      sdkImportRefusals(join(app, "../.."), [
        "apps/platform/src/error-tracking/probe.ts",
        "apps/platform/next.config.ts",
        "apps/platform/src/instrumentation.ts",
        "apps/platform/src/app/layout.tsx",
        "apps/platform/src/features/probe/probe.tsx",
        "apps/platform/src/shared/probe.ts",
        "packages/ui/src/components/probe/probe.tsx",
        "packages/api-client/src/probe.ts",
      ]),
    ).toEqual([
      "apps/platform/src/app/layout.tsx",
      "apps/platform/src/features/probe/probe.tsx",
      "apps/platform/src/instrumentation.ts",
      "apps/platform/src/shared/probe.ts",
      "packages/api-client/src/probe.ts",
      "packages/ui/src/components/probe/probe.tsx",
    ]);
  });
});
