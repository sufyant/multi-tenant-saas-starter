import { Language } from "@repo/api-client";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { z } from "zod";
import en from "../../../messages/en.json";
import { zodErrorMap } from "../../lib/zod-error-map";
import { PreferencesForm, type PreferencesFormProps } from "./preferences-form";

z.config({ customError: zodErrorMap });

// Each app holds the locale's format; the form reads it as the app's pages do.
const messages = { ...en, locale: { format: "en-IE" } };

// The app gives the generated schemas; this one has their shape and the names' rule (A7).
const name = z.string().trim().min(1).max(100);
const schema = z.object({
  firstName: name,
  lastName: name,
  language: z.enum(Language),
  timeZone: z.string().nullable(),
});

const rename = vi.fn(async () => ({}));
const changePreferences = vi.fn(async () => ({}));
const onSaved = vi.fn(async () => {});
const onFailed = vi.fn();
beforeEach(() => vi.clearAllMocks());

/** A refusal that names the fields, as Problem Details' `errors` does (F4). */
class Refusal extends Error {
  constructor(readonly fields: Record<string, string[]>) {
    super("refused");
  }
}

function renderForm(props: Partial<PreferencesFormProps> = {}) {
  return render(
    <NextIntlClientProvider
      locale="en"
      messages={messages}
      timeZone="Europe/Dublin"
    >
      <PreferencesForm
        values={{
          firstName: "Ayşe",
          lastName: "Yılmaz",
          language: Language.En,
          timeZone: "Europe/Istanbul",
        }}
        schema={schema}
        languages={Object.values(Language)}
        nameNote="Your name shows to the members of every organisation you belong to."
        rename={rename}
        changePreferences={changePreferences}
        fieldCodesOf={(error) =>
          error instanceof Refusal ? error.fields : null
        }
        onSaved={onSaved}
        onFailed={onFailed}
        {...props}
      />
    </NextIntlClientProvider>,
  );
}

const save = () =>
  userEvent.click(screen.getByRole("button", { name: "Save preferences" }));

async function retype(label: string, text: string) {
  const field = screen.getByRole("textbox", { name: label });
  await userEvent.clear(field);
  await userEvent.type(field, text);
}

describe("PreferencesForm", () => {
  it("PreferencesForm_Values_ShowsTheNamesAndTheNote", () => {
    renderForm();

    expect(screen.getByRole("textbox", { name: "First name" })).toHaveValue(
      "Ayşe",
    );
    expect(screen.getByRole("textbox", { name: "Last name" })).toHaveValue(
      "Yılmaz",
    );
    expect(
      screen.getByRole("heading", { name: "Your name" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "Your name shows to the members of every organisation you belong to.",
      ),
    ).toBeInTheDocument();
  });

  it("PreferencesForm_NameChanged_SendsTheTrimmedNamesOnly", async () => {
    renderForm();

    await retype("First name", "  Ayla ");
    await save();

    await vi.waitFor(() =>
      expect(onSaved).toHaveBeenCalledWith({
        names: { firstName: "Ayla", lastName: "Yılmaz" },
      }),
    );
    expect(rename).toHaveBeenCalledWith({
      firstName: "Ayla",
      lastName: "Yılmaz",
    });
    expect(changePreferences).not.toHaveBeenCalled();
  });

  it("PreferencesForm_NothingChanged_SendsNothing", async () => {
    renderForm();

    await retype("Last name", " Yılmaz ");
    await save();

    expect(rename).not.toHaveBeenCalled();
    expect(changePreferences).not.toHaveBeenCalled();
    expect(onSaved).not.toHaveBeenCalled();
  });

  it("PreferencesForm_WhitespaceName_SaysEnterYourFirstName", async () => {
    renderForm();

    await retype("First name", "   ");
    await save();

    expect(
      await screen.findByRole("textbox", { name: "First name" }),
    ).toHaveAccessibleDescription("Enter your first name.");
    expect(rename).not.toHaveBeenCalled();
  });

  it("PreferencesForm_RenameRefusedWithACode_ShowsItUnderTheField", async () => {
    rename.mockRejectedValueOnce(new Refusal({ lastName: ["too_long"] }));
    renderForm();

    await retype("Last name", "Kaya");
    await save();

    await vi.waitFor(() =>
      expect(
        screen.getByRole("textbox", { name: "Last name" }),
      ).toHaveAccessibleDescription("Use 1 to 100 characters."),
    );
    expect(onFailed).not.toHaveBeenCalled();
    expect(onSaved).not.toHaveBeenCalled();
  });

  it("PreferencesForm_RenameFailedWithoutAFieldCode_HandsTheErrorToTheApp", async () => {
    const failure = new Error("server");
    rename.mockRejectedValueOnce(failure);
    renderForm();

    await retype("First name", "Ayla");
    await save();

    await vi.waitFor(() => expect(onFailed).toHaveBeenCalledWith(failure));
  });

  it("PreferencesForm_RenameFailedChangePreferencesSaved_KeepsThePreferencesAndHandsTheErrorToTheApp", async () => {
    const failure = new Error("server");
    rename.mockRejectedValueOnce(failure);
    renderForm();

    await retype("First name", "Ayla");
    await userEvent.click(screen.getByRole("combobox", { name: "Time zone" }));
    await userEvent.type(
      await screen.findByRole("combobox", { name: "Search cities or zones" }),
      "Riyadh",
    );
    await userEvent.click(
      await screen.findByRole("option", { name: /^Riyadh/ }),
    );
    await save();

    await vi.waitFor(() => expect(onFailed).toHaveBeenCalledWith(failure));
    expect(changePreferences).toHaveBeenCalledWith({
      language: Language.En,
      timeZone: "Asia/Riyadh",
    });
    expect(onSaved).toHaveBeenCalledWith({
      preferences: { language: Language.En, timeZone: "Asia/Riyadh" },
    });
  });
});
