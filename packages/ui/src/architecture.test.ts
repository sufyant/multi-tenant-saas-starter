import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import {
  colorLiterals,
  contractEnumValues,
  dateFormattingOutsideFormatter,
  defaultBreakpointClasses,
  differingFiles,
  duplicateRuleIds,
  enumLiterals,
  enumValues,
  errorCodeMessages,
  generatedFileNames,
  messageKeySets,
  numericLayerClasses,
  physicalDirectionClasses,
  primaryTextClasses,
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

  it("F49_PackagesUi_UsesOnlyTheDesignsBreakpoints", () => {
    expect(defaultBreakpointClasses(src)).toEqual([]);
  });

  it("F53_PackagesUi_UsesNoNumericLayerClass", () => {
    expect(numericLayerClasses(src)).toEqual([]);
  });

  it("F20_PackagesUi_ColoursNoTextWithPrimary", () => {
    expect(primaryTextClasses(src)).toEqual([]);
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

  // Each app gives Account and security the same calls of the email change, which packages/ui does not read at run time (F62).
  it("F62_EmailChangeCalls_AreTheSameInBothApps", () => {
    const file = "src/features/account/use-email-change.ts";
    expect(readFileSync(join(packages, "../apps/web", file), "utf8")).toBe(
      readFileSync(join(packages, "../apps/platform", file), "utf8"),
    );
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

  // Each app holds the same live stream, because both read the generated client (F57, and the deviations table).
  it.each(["src/shared/notification-stream.tsx"])(
    "F57_%s_IsTheSameInBothApps",
    (file) => {
      expect(readFileSync(join(packages, "../apps/web", file), "utf8")).toBe(
        readFileSync(join(packages, "../apps/platform", file), "utf8"),
      );
    },
  );

  // Each app holds the same connection bar, the same error pages and the same root layout, because they read the client's caches,
  // the app's error tracking and its variables (F59, F27, and the deviations table).
  it.each([
    "src/shared/server-bar.tsx",
    "src/shared/error-page.tsx",
    "src/app/layout.tsx",
    "src/app/global-error.tsx",
  ])("F59_%s_IsTheSameInBothApps", (file) => {
    expect(readFileSync(join(packages, "../apps/web", file), "utf8")).toBe(
      readFileSync(join(packages, "../apps/platform", file), "utf8"),
    );
  });
});
