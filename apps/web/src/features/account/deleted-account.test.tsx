import { screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { renderWithProviders } from "../../../test/render";
import { DeletedAccount, deletedEmailKey } from "./deleted-account";

afterEach(() => sessionStorage.clear());

describe("DeletedAccount", () => {
  // The address comes from this tab's storage, never from the address bar (the privacy rules), and it is read once.
  it("DeletedAccount_AfterTheDeletion_NamesTheAddressOnceAndOffersSignIn", async () => {
    sessionStorage.setItem(deletedEmailKey, "lena@northwind.example");

    renderWithProviders(<DeletedAccount />);

    expect(
      await screen.findByRole("heading", { name: "Your account was deleted" }),
    ).toBeInTheDocument();
    expect(screen.getByText("lena@northwind.example")).toBeInTheDocument();
    expect(sessionStorage.getItem(deletedEmailKey)).toBeNull();
    expect(screen.getByRole("link", { name: "Go to sign-in" })).toHaveAttribute(
      "href",
      "/sign-in",
    );
  });

  it("DeletedAccount_WithoutAStoredAddress_SaysYourAddress", async () => {
    renderWithProviders(<DeletedAccount />);

    expect(
      await screen.findByText(/We sent a confirmation to your address\./),
    ).toBeInTheDocument();
  });
});
