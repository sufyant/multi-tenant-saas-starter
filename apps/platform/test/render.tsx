import { configureApiClient } from "@repo/api-client";
import { Toaster } from "@repo/ui/components/toast";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { NuqsAdapter } from "nuqs/adapters/react";
import type { ReactNode } from "react";
import { messages } from "@/i18n/messages";

/**
 * The test's origin. The mutator needs an absolute base in Node; MSW answers on it. It is an address that refuses a connection at
 * once: MSW passes an idle connection through to it after an aborted request, and a connection that takes a name lookup to fail
 * fails the next mocked request on it (mswjs/interceptors#881, `render.test.ts`). Nothing listens on port 47: on Linux, as on CI,
 * no ordinary process may listen below 1024. Fetch does not block the port.
 */
export const origin = "https://127.0.0.1:47";

/**
 * Renders a screen with the providers of the root layout and its toasts, in a language of the contract. nuqs reads and writes
 * jsdom's own address and history, so a test can go back and forward.
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
        <NuqsAdapter>{ui}</NuqsAdapter>
      </QueryClientProvider>
      <Toaster dir="ltr" closeLabel="Close" regionLabel="Notifications" />
    </NextIntlClientProvider>,
  );
}

/**
 * A text matcher for a sentence whose name sits in its own element (`<bdi>`). It matches what `getByText` matches, an element's own
 * text, with the text of the `bdi` elements it holds; else the innermost element whose whole text is the sentence.
 */
export const sentence =
  (text: string) => (_content: string, element: Element | null) => {
    // Testing Library's default normaliser: trimmed, with each run of spaces as one.
    const normal = (value: string | null) =>
      (value ?? "").trim().replace(/\s+/g, " ");
    if (!element) return false;
    // The element's own text, with the names it isolates: a banner holds its title and its sentence side by side.
    const own = Array.from(element.childNodes)
      .filter(
        (node) => node.nodeType === Node.TEXT_NODE || node.nodeName === "BDI",
      )
      .map((node) => node.textContent)
      .join("");
    return (
      normal(own) === text ||
      (normal(element.textContent) === text &&
        !Array.from(element.children).some(
          (child) => normal(child.textContent) === text,
        ))
    );
  };
