import { renderHook } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import type { Environment } from "../../lib/environment";
import { AppFrameProvider } from "../app-frame";
import { useTabTitleText } from "./use-tab-title-text";

const titleIn = (environment: Environment) => {
  const wrapper = ({ children }: { children: ReactNode }) => (
    <NextIntlClientProvider locale="en" messages={en}>
      <AppFrameProvider value={{ environment }}>{children}</AppFrameProvider>
    </NextIntlClientProvider>
  );
  return renderHook(() => useTabTitleText("Starter"), { wrapper }).result
    .current;
};

describe("useTabTitleText", () => {
  it("UseTabTitleText_ProductionWithUnread_StartsWithTheCount", () => {
    expect(titleIn({ kind: "production" })("Members", 2)).toBe(
      "(2) Members · Starter",
    );
  });

  it("UseTabTitleText_NoUnread_LeavesTheCountOut", () => {
    expect(titleIn({ kind: "production" })("Members", 0)).toBe(
      "Members · Starter",
    );
  });

  it("UseTabTitleText_Staging_StartsWithItsMark", () => {
    expect(titleIn({ kind: "staging" })("Members", 2)).toBe(
      "[Staging] (2) Members · Starter",
    );
  });
});
