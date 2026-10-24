import { ErrorCode } from "@repo/api-client";
import { renderHook } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import en from "../../messages/en.json";
import { useFailureText } from "./use-failure-text";

// Each app holds the locale's format; the hook reads it as the app's pages do.
const messages = { ...en, locale: { format: "en-IE" } };
const reference = "4bf92f3577b34da6a3ce929d0e0e4736";

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date("2026-10-16T13:32:00Z"));
});
afterEach(() => vi.useRealTimers());

const wrapper = ({ children }: { children: ReactNode }) => (
  <NextIntlClientProvider
    locale="en"
    messages={messages}
    timeZone="Europe/Dublin"
  >
    {children}
  </NextIntlClientProvider>
);

describe("useFailureText", () => {
  it("UseFailureText_Problem_IsItsCodesMessageAndItsReference", () => {
    const { result } = renderHook(() => useFailureText(), { wrapper });

    expect(result.current({ code: ErrorCode.TenantNotFound, reference })).toBe(
      "This organisation does not exist, or you are not a member of it. Ref 4bf92f3577b34da6a3ce929d0e0e4736 · 13:32 UTC",
    );
  });

  it("UseFailureText_NoProblem_IsTheMessageOfAServerError", () => {
    const { result } = renderHook(() => useFailureText(), { wrapper });

    expect(result.current(null)).toBe(en.errors.server.error);
  });
});
