import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Input } from "./input";

describe("Input", () => {
  it("Input_WithLabel_IsNamedByIt", () => {
    render(<Input label="Tenant name" defaultValue="Northwind Traders" />);

    expect(screen.getByRole("textbox", { name: "Tenant name" })).toHaveValue(
      "Northwind Traders",
    );
  });

  it("Input_WithError_ReplacesTheHintAndIsInvalid", () => {
    render(
      <Input
        label="Email"
        type="email"
        hint="The invitation goes here."
        error="Enter an email address."
      />,
    );

    const field = screen.getByRole("textbox", { name: "Email" });
    expect(field).toHaveAttribute("aria-invalid", "true");
    expect(field).toHaveAccessibleDescription("Enter an email address.");
    expect(
      screen.queryByText("The invitation goes here."),
    ).not.toBeInTheDocument();
  });

  it("Input_EmailInRtl_StaysLeftToRight", () => {
    render(
      <div dir="rtl">
        <Input
          label="البريد الإلكتروني"
          type="email"
          defaultValue="ali@northwind.io"
        />
      </div>,
    );

    expect(
      screen.getByRole("textbox", { name: "البريد الإلكتروني" }),
    ).toHaveAttribute("dir", "ltr");
  });

  it("Input_Locked_IsReadOnlyWithItsReason", () => {
    render(
      <Input
        label="Slug"
        defaultValue="northwind"
        lockedReason="Used in links. It can't be changed."
      />,
    );

    expect(screen.getByRole("textbox", { name: "Slug" })).toHaveAttribute(
      "readonly",
    );
    expect(
      screen.getByRole("button", {
        name: "Used in links. It can't be changed.",
      }),
    ).toBeInTheDocument();
  });
});
