import "@testing-library/jest-dom/vitest";
import { cleanup, configure } from "@testing-library/react";
import { afterEach } from "vitest";

afterEach(() => {
  cleanup();
});

// A test has one time limit: testTimeout in vitest.config.ts. Testing Library's findBy and waitFor stop after 1 s by default,
// a second and shorter limit that CI's load passes. They wait up to the same 15 s (F50).
configure({ asyncUtilTimeout: 15_000 });

// jsdom has no matchMedia. sonner and next-themes read it; every query answers "no match", as a desktop with a fine pointer.
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

// jsdom has no ResizeObserver or scrollIntoView. cmdk calls both; the tests do not measure layout.
globalThis.ResizeObserver ??= class {
  observe() {}
  unobserve() {}
  disconnect() {}
};
Element.prototype.scrollIntoView ??= () => {};

// jsdom has no elementFromPoint. input-otp calls it to find a password manager's badge beside the code.
document.elementFromPoint ??= () => null;
