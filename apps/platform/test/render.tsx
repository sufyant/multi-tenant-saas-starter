import { configureApiClient } from "@repo/api-client";
import { Toaster } from "@repo/ui/components/toast";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import type { ReactNode } from "react";
import { messages } from "@/i18n/messages";

/** The test's origin. The mutator needs an absolute base in Node; MSW answers on it. */
export const origin = "https://web.example.test";

/** Renders a screen with the providers of the root layout and its toasts, in a language of the contract. */
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
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
      <Toaster dir="ltr" closeLabel="Close" regionLabel="Notifications" />
    </NextIntlClientProvider>,
  );
}
