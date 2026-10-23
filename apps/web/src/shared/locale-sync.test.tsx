import { Language } from "@repo/api-client";
import { waitFor } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { setLocale } from "@/i18n/set-locale";
import { renderWithProviders } from "../../test/render";
import { LocaleSync } from "./locale-sync";

const clerk = vi.hoisted(() => ({ loaded: false }));
const refresh = vi.hoisted(() => vi.fn());

vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({ loaded: clerk.loaded }),
}));
vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh }) }));
vi.mock("@/i18n/set-locale", () => ({ setLocale: vi.fn(async () => {}) }));

beforeEach(() => {
  vi.mocked(setLocale).mockClear();
  refresh.mockClear();
});

// In a development instance, Clerk's script deletes and writes the dev browser cookie again on each answer of its Frontend
// API while it loads. A refresh in that moment reaches the proxy without the cookie, and Clerk signs the request out (F13).
it("LocaleSync_ClerkStillLoading_KeepsTheLanguage", async () => {
  clerk.loaded = false;

  renderWithProviders(<LocaleSync language={Language.Ar} />);

  await new Promise((resolve) => setTimeout(resolve, 0));
  expect(setLocale).not.toHaveBeenCalled();
  expect(refresh).not.toHaveBeenCalled();
});

it("LocaleSync_ClerkLoaded_SetsTheLanguageAndRendersAgain", async () => {
  clerk.loaded = true;

  renderWithProviders(<LocaleSync language={Language.Ar} />);

  await waitFor(() => expect(refresh).toHaveBeenCalledOnce());
  expect(setLocale).toHaveBeenCalledWith(Language.Ar);
});
