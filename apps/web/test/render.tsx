import { configureApiClient } from "@repo/api-client";
import { AppFrameProvider } from "@repo/ui/components/app-frame";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { NuqsAdapter } from "nuqs/adapters/react";
import type { ReactNode } from "react";
import { messages } from "@/i18n/messages";

/** The test's origin. The mutator needs an absolute base in Node; MSW answers on it. */
export const origin = "https://web.example.test";

/** The test's `WEB_APP_URL`: the host of every organisation's address. */
export const webAppUrl = "https://app.example.test";

/**
 * Renders a screen with the providers of the root layout, in a language of the contract. nuqs reads and writes jsdom's own
 * address and history, so a test can go back and forward.
 */
export function renderWithProviders(
  ui: ReactNode,
  locale: keyof typeof messages = "en",
) {
  configureApiClient({ baseUrl: `${origin}/api` });
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <NextIntlClientProvider
      locale={locale}
      messages={messages[locale]}
      timeZone="Europe/Dublin"
    >
      <QueryClientProvider client={queryClient}>
        <NuqsAdapter>
          <AppFrameProvider
            value={{ environment: { kind: "production" }, webAppUrl }}
          >
            {ui}
          </AppFrameProvider>
        </NuqsAdapter>
      </QueryClientProvider>
    </NextIntlClientProvider>,
  );
}
