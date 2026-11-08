import "@testing-library/jest-dom/vitest";
import { dismissToasts } from "@repo/ui/components/toast";
import { zodErrorMap } from "@repo/ui/lib/zod-error-map";
import { cleanup, configure, prettyDOM } from "@testing-library/react";
import { afterEach, beforeEach, vi } from "vitest";
import { z } from "zod";

afterEach(() => {
  cleanup();
  dismissToasts();
});

// A test has one time limit: testTimeout in vitest.config.mts. Testing Library's findBy and waitFor stop after 1 s by default,
// a second and shorter limit that CI's load passes. They wait up to the same 15 s (F50).
configure({ asyncUtilTimeout: 15_000 });

// So a test that waits for what never comes fails at that limit without naming what it waited for. Each test therefore records its
// requests, and a failed test writes them and the page as it was (F50). The recorder wraps fetch before each test, over the fetch
// that MSW patched when the file's server started. Vitest runs afterEach hooks in reverse order, so this one runs before the
// cleanup above and still sees the page.
type Sent = { at: number; call: string; answer: string };
let sent: Sent[] = [];
let unwrapped = globalThis.fetch;
beforeEach(() => {
  sent = [];
  unwrapped = globalThis.fetch;
  const started = performance.now();
  globalThis.fetch = (input, init) => {
    const request = input instanceof Request ? input : null;
    const url = new URL(request?.url ?? String(input), window.location.href);
    const record: Sent = {
      at: Math.round(performance.now() - started),
      call: `${init?.method ?? request?.method ?? "GET"} ${url.pathname}${url.search}`,
      answer: "no answer yet",
    };
    sent.push(record);
    return unwrapped(input, init).then(
      (response) => {
        record.answer = `${response.status}`;
        return response;
      },
      (error: unknown) => {
        record.answer = `failed: ${error}`;
        throw error;
      },
    );
  };
});
afterEach(({ task }) => {
  globalThis.fetch = unwrapped;
  if (task.result?.state !== "fail") return;
  console.error(
    [
      `The requests of "${task.name}", in ms from its start:`,
      ...sent.map(({ at, call, answer }) => `  ${at} ${call} -> ${answer}`),
      "The page when it failed:",
      prettyDOM(document.body) || "(empty)",
    ].join("\n"),
  );
});

// jsdom has no matchMedia, ResizeObserver or scrollIntoView; next-themes, sonner and cmdk read them.
Object.defineProperty(window, "matchMedia", {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  }),
});
globalThis.ResizeObserver ??= class {
  observe() {}
  unobserve() {}
  disconnect() {}
};
Element.prototype.scrollIntoView ??= () => {};

// jsdom has no elementFromPoint. input-otp calls it to find a password manager's badge beside the code.
document.elementFromPoint ??= () => null;

// The app installs the one Zod error map in its providers (F12); a form under test needs it too.
z.config({ customError: zodErrorMap });

// Next.js's cookie store exists only in a request; the server actions of the pages write into this fake one.
vi.mock("next/headers", async () => {
  const { cookieStore } = await import("./cookies");
  return { cookies: async () => cookieStore };
});
afterEach(async () => {
  (await import("./cookies")).resetCookies();
});
