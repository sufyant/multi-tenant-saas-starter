import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { GlobeIcon } from "../icon";
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
  // The Select of the Components page draws a chevron down, as the Profile's language and time zone (frame 5.01, step 36).
  it("Combobox_Field_EndsWithAChevronDown", () => {
    render(<Combobox {...props} options={languages} value="en-IE" />);

    const trigger = screen.getByRole("combobox", {
      name: "Language and format",
    });
    expect(trigger.lastElementChild).toHaveClass("lucide-chevron-down");
  });
  // The Auth footer's language (frames 1.01 and 1.35): the globe, the name and a chevron at the end.
  it("Combobox_Compact_HasTheGlobeTheNameAndAChevron", () => {
    render(
      <Combobox
        {...props}
        variant="compact"
        icon={GlobeIcon}
        options={languages}
        value="en-IE"
      />,
    );

    const trigger = screen.getByRole("combobox", {
      name: "Language and format",
    });
    expect(trigger.firstElementChild).toHaveClass("lucide-globe");
    expect(trigger).toHaveTextContent(/^English \(Ireland\)$/);
    expect(trigger.lastElementChild).toHaveClass("lucide-chevron-down");
  });

  // Frame 1.35: the search field starts with its icon, and العربية keeps its direction but starts where the list starts.
  it("Combobox_Open_ShowsTheSearchIconAndStartsEachOptionAtTheListsStart", async () => {
    const user = userEvent.setup();
    render(<Combobox {...props} options={languages} value="en-IE" />);

    await user.click(
      screen.getByRole("combobox", { name: "Language and format" }),
    );

    const search = await screen.findByRole("combobox", {
      name: "Search languages",
    });
    expect(search.previousElementSibling).toHaveClass("lucide-search");
    const arabic = within(
      await screen.findByRole("option", { name: /العربية/ }),
    ).getByText("العربية");
    expect(arabic).toHaveAttribute("dir", "rtl");
    expect(arabic.parentElement).not.toHaveAttribute("dir");
    expect(arabic.parentElement).toHaveClass("flex-1");
  });
});
