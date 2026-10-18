import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { Combobox } from "./combobox";

const languages = [
  { id: "en-IE", label: "English (Ireland)", hint: "16/10/2026" },
  {
    id: "tr-TR",
    label: "Türkçe",
    hint: "16.10.2026",
    lang: "tr",
    keywords: ["Turkish"],
  },
  {
    id: "ar-SA",
    label: "العربية",
    hint: "16/10/2026",
    lang: "ar",
    dir: "rtl" as const,
    keywords: ["Arabic"],
  },
];

const props = {
  label: "Language and format",
  searchPlaceholder: "Search languages",
  emptyLabel: "No language found",
};

describe("Combobox", () => {
  it("Combobox_TypeTheNameInTheUiLanguage_FindsTheOptionInItsOwnScript", async () => {
    render(<Combobox {...props} options={languages} value="en-IE" />);

    await userEvent.click(
      screen.getByRole("combobox", { name: "Language and format" }),
    );
    await userEvent.type(
      await screen.findByRole("combobox", { name: "Search languages" }),
      "arab",
    );

    const option = await screen.findByRole("option", { name: /العربية/ });
    expect(option.querySelector("[lang]")).toHaveAttribute("lang", "ar");
    expect(
      screen.queryByRole("option", { name: /Türkçe/ }),
    ).not.toBeInTheDocument();
  });

  it("Combobox_PickAnOption_ReportsItsId", async () => {
    const onValueChange = vi.fn();
    render(
      <Combobox
        {...props}
        options={languages}
        value="en-IE"
        onValueChange={onValueChange}
      />,
    );

    await userEvent.click(
      screen.getByRole("combobox", { name: "Language and format" }),
    );
    await userEvent.click(
      await screen.findByRole("option", { name: /Türkçe/ }),
    );

    expect(onValueChange).toHaveBeenCalledWith("tr-TR");
  });

  it("Combobox_InRtl_ShowsTheValue", () => {
    render(
      <div dir="rtl">
        <Combobox {...props} label="اللغة" options={languages} value="ar-SA" />
      </div>,
    );

    expect(screen.getByRole("combobox", { name: "اللغة" })).toHaveTextContent(
      "العربية",
    );
  });
});
