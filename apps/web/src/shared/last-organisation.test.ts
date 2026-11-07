import { beforeEach, describe, expect, it, vi } from "vitest";
import { lastOrganisationCookie } from "./last-organisation-cookie";

// The setup's stand-in of this module is for the pages; this test reads the real one.
vi.unmock("@/shared/last-organisation");

// Next.js's cookie store is not ours, so the test fakes it (Khorikov, Chapter 9).
const store = vi.hoisted(() => ({
  value: undefined as string | undefined,
  set: vi.fn(),
  delete: vi.fn(),
}));
vi.mock("next/headers", () => ({
  cookies: async () => ({
    get: () => (store.value ? { value: store.value } : undefined),
    set: store.set,
    delete: store.delete,
  }),
}));

const { forgetOrganisation, rememberOrganisation } = await import(
  "@/shared/last-organisation"
);

beforeEach(() => {
  store.value = undefined;
  store.set.mockClear();
  store.delete.mockClear();
});

describe("last organisation", () => {
  // The roadmap, 37a-frontend-1: the slug only, HttpOnly, Secure, SameSite=Lax.
  it("RememberOrganisation_ASlug_KeepsOnlyTheSlugInAnHttpOnlySecureLaxCookie", async () => {
    await rememberOrganisation("northwind");

    expect(store.set).toHaveBeenCalledExactlyOnceWith(
      lastOrganisationCookie,
      "northwind",
      expect.objectContaining({
        httpOnly: true,
        secure: true,
        sameSite: "lax",
        path: "/",
      }),
    );
  });

  // Setting a cookie in a server action renders the page again, so the same slug writes nothing.
  it("RememberOrganisation_TheSlugItHolds_WritesNothing", async () => {
    store.value = "northwind";

    await rememberOrganisation("northwind");

    expect(store.set).not.toHaveBeenCalled();
  });

  // A server action is a public endpoint: a value that is no slug never reaches the cookie.
  it("RememberOrganisation_NotASlug_WritesNothing", async () => {
    await rememberOrganisation("north wind; path=/");

    expect(store.set).not.toHaveBeenCalled();
  });

  it("ForgetOrganisation_ACookie_DeletesIt", async () => {
    store.value = "atlas";

    await forgetOrganisation();

    expect(store.delete).toHaveBeenCalledExactlyOnceWith(
      lastOrganisationCookie,
    );
  });
});
