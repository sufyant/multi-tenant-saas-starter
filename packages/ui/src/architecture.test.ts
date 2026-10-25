import { join } from "node:path";
import { describe, expect, it } from "vitest";
import {
  colorLiterals,
  contractEnumValues,
  dateFormattingOutsideFormatter,
  differingFiles,
  duplicateRuleIds,
  enumLiterals,
  enumValues,
  errorCodeMessages,
  generatedFileNames,
  messageKeySets,
  numericLayerClasses,
  physicalDirectionClasses,
  testFolders,
  testsBesideTheirFile,
  userFacingLiterals,
} from "../test/architecture-rules";

// The rules of docs/frontend/ARCHITECTURE.md for packages/ui, and the generated-file names of every package.
const src = join(import.meta.dirname);
const packages = join(import.meta.dirname, "../..");
const messages = join(import.meta.dirname, "../messages");
const generatedSchemas = join(
  packages,
  "api-client/src/generated/endpoints.schemas.gen.ts",
);
const languages = enumValues(generatedSchemas, "Language");

describe("architecture of packages/ui", () => {
  it("F17_PackagesUi_UsesNoPhysicalDirectionClass", () => {
    expect(physicalDirectionClasses(src)).toEqual([]);
  });

  it("F21_PackagesUi_UsesNoColourLiteral", () => {
    expect(colorLiterals(src)).toEqual([]);
  });

  it("F53_PackagesUi_UsesNoNumericLayerClass", () => {
    expect(numericLayerClasses(src)).toEqual([]);
  });

  it("F25_PackagesUi_WritesNoUserFacingLiteral", () => {
    expect(
      userFacingLiterals(src, {
        ignore: ["components/dev-components-page.tsx"],
      }),
    ).toEqual([]);
  });

  it("F35_PackagesUi_HasNoTestsFolder", () => {
    expect(testFolders(join(src, ".."))).toEqual([]);
  });

  it("F35_PackagesUi_PutsEachTestBesideItsFile", () => {
    expect(testsBesideTheirFile(join(src, ".."))).toEqual([]);
  });

  it("F32_Packages_NameEveryGeneratedFileGen", () => {
    expect(generatedFileNames(packages)).toEqual([]);
  });

  it("RuleIds_FrontendArchitecture_GivesEachRuleOneId", () => {
    expect(
      duplicateRuleIds(join(packages, "../docs/frontend/ARCHITECTURE.md")),
    ).toEqual([]);
  });

  it("F15_SharedMessages_HaveTheSameKeysInEveryLanguage", () => {
    expect(messageKeySets(messages, languages)).toEqual([]);
  });

  it("F16_SharedMessages_HaveAMessageForEveryErrorCode", () => {
    expect(
      errorCodeMessages(
        messages,
        languages,
        enumValues(generatedSchemas, "ErrorCode"),
      ),
    ).toEqual([]);
  });

  it("F18_PackagesUi_FormatsDatesOnlyInFormatDateTime", () => {
    expect(dateFormattingOutsideFormatter(src)).toEqual([]);
  });

  it("F3_PackagesUi_WritesNoEnumValueAsALiteral", () => {
    expect(enumLiterals(src, contractEnumValues(generatedSchemas))).toEqual([]);
  });

  // Each app holds the same error tracking code, because only an app may import the SDK (F55, and the deviations table).
  it("F55_ErrorTracking_IsTheSameInBothApps", () => {
    expect(
      differingFiles(
        join(packages, "../apps/web/src/error-tracking"),
        join(packages, "../apps/platform/src/error-tracking"),
      ),
    ).toEqual([]);
  });
});
