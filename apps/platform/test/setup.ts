import "@testing-library/jest-dom/vitest";
import { dismissToasts } from "@repo/ui/components/toast";
import { zodErrorMap } from "@repo/ui/lib/zod-error-map";
import { cleanup, configure } from "@testing-library/react";
import { afterEach } from "vitest";
import { z } from "zod";

afterEach(() => {
  cleanup();
  dismissToasts();
});

// A test has one time limit: testTimeout in vitest.config.mts. Testing Library's findBy and waitFor stop after 1 s by default,
// a second and shorter limit that CI's load passes. They wait up to the same 15 s (F50).
configure({ asyncUtilTimeout: 15_000 });

// The one Zod error map gives message keys (F12), as the root layout's providers install it.
z.config({ customError: zodErrorMap });

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
