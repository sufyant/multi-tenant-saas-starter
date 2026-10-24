import "@testing-library/jest-dom/vitest";
import { dismissToasts } from "@repo/ui/components/toast";
import { zodErrorMap } from "@repo/ui/lib/zod-error-map";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";
import { z } from "zod";

afterEach(() => {
  cleanup();
  dismissToasts();
});

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
