import "@testing-library/jest-dom/vitest";
import { zodErrorMap } from "@repo/ui/lib/zod-error-map";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";
import { z } from "zod";

afterEach(() => {
  cleanup();
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
