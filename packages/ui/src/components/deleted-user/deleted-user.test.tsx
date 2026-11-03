import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { describe, expect, it } from "vitest";
import ar from "../../../messages/ar.json";
import en from "../../../messages/en.json";
import { DeletedUser } from "./deleted-user";

describe("DeletedUser", () => {
  it("DeletedUser_English_ShowsDeletedUserMuted", () => {
    render(
      <NextIntlClientProvider locale="en" messages={en}>
        <DeletedUser />
      </NextIntlClientProvider>,
    );

    expect(screen.getByText("Deleted user")).toHaveClass(
      "text-muted-foreground",
    );
  });

  it("DeletedUser_Arabic_ShowsTheArabicText", () => {
    render(
      <NextIntlClientProvider locale="ar" messages={ar}>
        <DeletedUser />
      </NextIntlClientProvider>,
    );

    expect(screen.getByText("مستخدم محذوف")).toBeInTheDocument();
  });
});
