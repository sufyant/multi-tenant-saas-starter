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

const filter = {
  label: "Filter by role",
  variant: "filter" as const,
  options: [
    { id: "all", label: "All roles" },
    { id: "billing", label: "Role: Billing lead" },
  ],
  searchPlaceholder: "Search roles",
  emptyLabel: "No role found",
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

  it("Combobox_Error_ReplacesTheHintAndMarksTheFieldInvalid", () => {
    render(
      <Combobox
        {...props}
        options={languages}
        value="en-IE"
        hint="Dates use this format."
        error="Choose a language from the list."
      />,
    );

    const field = screen.getByRole("combobox", { name: "Language and format" });
    expect(field).toHaveAttribute("aria-invalid", "true");
    expect(field).toHaveAccessibleDescription(
      "Choose a language from the list.",
    );
    expect(
      screen.queryByText("Dates use this format."),
    ).not.toBeInTheDocument();
  });

  it("Combobox_Hint_DescribesTheField", () => {
    render(
      <Combobox
        {...props}
        options={languages}
        value="en-IE"
        hint="Dates use this format."
      />,
    );

    expect(
      screen.getByRole("combobox", { name: "Language and format" }),
    ).toHaveAccessibleDescription("Dates use this format.");
  });
  it("Combobox_Filter_NamesTheWholeSetWhenNothingIsChosen", () => {
    render(<Combobox {...filter} value="all" />);

    const trigger = screen.getByRole("combobox", { name: "Filter by role" });
    expect(trigger).toHaveTextContent(/^All roles$/);
    expect(screen.getByText("Filter by role")).toHaveClass("sr-only");
  });

  it("Combobox_FilterWithAChoice_NamesTheFilterAndTheValue", () => {
    render(<Combobox {...filter} value="billing" />);

    expect(
      screen.getByRole("combobox", { name: "Filter by role" }),
    ).toHaveTextContent(/^Role: Billing lead$/);
  });

  // jsdom draws nothing, so the look is the classes of the tokens; Playwright compares the drawn heights on Members.
  it("Combobox_Filter_HasTheSearchFieldsBorderAndHeightAndAChevronAtTheEnd", () => {
    render(<Combobox {...filter} value="all" />);

    const trigger = screen.getByRole("combobox", { name: "Filter by role" });
    expect(trigger).toHaveClass(
      "h-control-md",
      "pointer-coarse:h-control-lg",
      "border",
      "border-input",
      "min-w-35",
    );
    expect(trigger.lastElementChild).toHaveClass("lucide-chevron-down");
  });
});
