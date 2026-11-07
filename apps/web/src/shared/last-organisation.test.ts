import { describe, expect, it } from "vitest";
import { cookieStore } from "../../test/cookies";
import { forgetOrganisation, rememberOrganisation } from "./last-organisation";
import { lastOrganisationCookie } from "./last-organisation-cookie";

describe("last organisation", () => {
  // The roadmap, 37a-frontend-1: the slug only, HttpOnly, Secure, SameSite=Lax.
  it("RememberOrganisation_ASlug_KeepsOnlyTheSlugInAnHttpOnlySecureLaxCookie", async () => {
    await rememberOrganisation("northwind");

    expect(cookieStore.set).toHaveBeenCalledExactlyOnceWith(
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
    cookieStore.values.set(lastOrganisationCookie, "northwind");

    await rememberOrganisation("northwind");

    expect(cookieStore.set).not.toHaveBeenCalled();
  });

  // A server action is a public endpoint: a value that is no slug never reaches the cookie.
  it("RememberOrganisation_NotASlug_WritesNothing", async () => {
    await rememberOrganisation("north wind; path=/");

    expect(cookieStore.set).not.toHaveBeenCalled();
  });

  it("ForgetOrganisation_ACookie_DeletesIt", async () => {
    cookieStore.values.set(lastOrganisationCookie, "atlas");

    await forgetOrganisation();

    expect(cookieStore.delete).toHaveBeenCalledExactlyOnceWith(
      lastOrganisationCookie,
    );
  });
});
