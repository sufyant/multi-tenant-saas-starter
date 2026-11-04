import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import { NotFound, type NotFoundProps } from "./not-found";

const renderNotFound = (props: NotFoundProps) =>
  render(
    <NextIntlClientProvider locale="en" messages={en}>
      <NotFound {...props} />
    </NextIntlClientProvider>,
  );

describe("NotFound", () => {
  it("NotFound_OldLink_SaysSoAndGoesHome", () => {
    renderNotFound({ kind: "page", href: "/northwind" });

    expect(
      screen.getByRole("heading", { name: "Page not found" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "This link is old, or the page has moved. Go to Home and find it from there.",
      ),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Go to Home" })).toHaveAttribute(
      "href",
      "/northwind",
    );
  });

  it("NotFound_OldLink_ShowsTheSearchIconAndASecondaryButton", () => {
    const { container } = renderNotFound({ kind: "page", href: "/northwind" });

    expect(container.querySelector("svg.lucide-search-x")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Go to Home" })).toHaveClass(
      "border-input",
      "bg-card",
    );
  });

  // 2.40 and 2.43: the whole content of the page, 120 px below the top bar from bp-tablet.
  it("NotFound_OldLink_FillsThePage", () => {
    renderNotFound({ kind: "page", href: "/northwind" });

    expect(
      screen.getByRole("heading", { name: "Page not found" }).parentElement,
    ).toHaveClass("tablet:pt-30");
  });

  it("NotFound_SignedOut_KeepsTheFramesSpacing", () => {
    renderNotFound({ kind: "signed-out", href: "/sign-in" });

    expect(
      screen.getByRole("heading", { name: "Page not found" }).parentElement,
    ).not.toHaveClass("tablet:pt-30");
  });

  it("NotFound_Organisation_NamesTheAddress", () => {
    renderNotFound({
      kind: "organisation",
      address: "app.starter.example/northwnd",
      href: "/",
    });

    expect(
      screen.getByRole("heading", { name: "Organisation not found" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("app.starter.example/northwnd"),
    ).toBeInTheDocument();
  });

  it("NotFound_SignedOut_OffersSignIn", () => {
    renderNotFound({ kind: "signed-out", href: "/sign-in" });

    expect(
      screen.getByText(
        "This page does not exist, or you need to sign in to see it.",
      ),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sign in" })).toHaveAttribute(
      "href",
      "/sign-in",
    );
  });
});
