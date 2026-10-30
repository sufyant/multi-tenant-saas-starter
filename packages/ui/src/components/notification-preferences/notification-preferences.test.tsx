import { NotificationCategory } from "@repo/api-client";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NextIntlClientProvider } from "next-intl";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ar from "../../../messages/ar.json";
import en from "../../../messages/en.json";
import { expectVisibleToast } from "../../../test/toast";
import { Toaster } from "../toast";
import {
  NotificationPreferences,
  type NotificationPreferencesProps,
} from "./notification-preferences";

const save = vi.fn(async () => {});
const onSaved = vi.fn(async () => {});
const onFailed = vi.fn();
beforeEach(() => vi.clearAllMocks());

const owner: NotificationPreferencesProps["values"] = {
  enabled: true,
  categories: [
    { category: NotificationCategory.Access, enabled: false },
    { category: NotificationCategory.MemberInvitations, enabled: false },
    { category: NotificationCategory.TenantPlans, enabled: false },
  ],
};

function renderForm(
  props: Partial<NotificationPreferencesProps> = {},
  locale: "en" | "ar" = "en",
) {
  return render(
    <NextIntlClientProvider
      locale={locale}
      messages={locale === "en" ? en : ar}
      timeZone="Europe/Dublin"
    >
      <NotificationPreferences
        values={owner}
        removedNote
        save={save}
        onSaved={onSaved}
        onFailed={onFailed}
        {...props}
      />
      <Toaster dir="ltr" closeLabel="Close" regionLabel="Notifications" />
    </NextIntlClientProvider>,
  );
}

const main = () => screen.getByRole("switch", { name: "Email notifications" });
const category = (name: string) => screen.getByRole("switch", { name });

describe("NotificationPreferences", () => {
  it("NotificationPreferences_NeverChosen_ShowsTheMainSwitchOnAndEveryCategoryOff", () => {
    renderForm();

    expect(main()).toBeChecked();
    expect(
      screen
        .getAllByRole("switch")
        .map((control) => control.getAttribute("aria-checked")),
    ).toEqual(["true", "false", "false", "false"]);
  });

  it("NotificationPreferences_Categories_ShowsOnlyTheListedOnesWithTheirDescriptions", () => {
    renderForm({
      values: {
        enabled: true,
        categories: [
          { category: NotificationCategory.PlatformTenants, enabled: true },
        ],
      },
      removedNote: false,
    });

    expect(category("Platform")).toBeChecked();
    expect(
      screen.getByText(
        "When an organisation gets its first Owner, changes status or is deleted.",
      ),
    ).toBeVisible();
    expect(screen.queryByRole("switch", { name: "Your access" })).toBeNull();
  });

  it("NotificationPreferences_AccessListed_SaysRemovalAlwaysEmails", () => {
    renderForm();

    expect(
      screen.getByText(
        "We always email you if you are removed from an organisation.",
      ),
    ).toBeVisible();
  });

  it("NotificationPreferences_AccessNotListed_SaysNothingOfRemoval", () => {
    renderForm({ removedNote: false });

    expect(
      screen.queryByText(
        "We always email you if you are removed from an organisation.",
      ),
    ).toBeNull();
  });

  it("NotificationPreferences_MainOff_ShowsEveryCategoryOffAndDisabled", async () => {
    renderForm({
      values: {
        ...owner,
        categories: [{ category: NotificationCategory.Access, enabled: true }],
      },
    });

    await userEvent.click(main());

    expect(category("Your access")).not.toBeChecked();
    expect(category("Your access")).toBeDisabled();
    expect(
      screen.getByText("Turn on email notifications to choose categories."),
    ).toBeVisible();
  });

  it("NotificationPreferences_MainOnAgain_BringsEachCategoryBackAsSaved", async () => {
    renderForm({
      values: {
        enabled: false,
        categories: [
          { category: NotificationCategory.Access, enabled: true },
          { category: NotificationCategory.TenantPlans, enabled: false },
        ],
      },
    });

    await userEvent.click(main());

    expect(category("Your access")).toBeChecked();
    expect(category("Organisation plan")).not.toBeChecked();
    expect(category("Your access")).toBeEnabled();
  });

  it("NotificationPreferences_Save_SendsTheMainSwitchAndEveryCategory", async () => {
    renderForm();

    await userEvent.click(category("Members"));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(save).toHaveBeenCalledExactlyOnceWith({
      enabled: true,
      categories: [
        { category: NotificationCategory.Access, enabled: false },
        { category: NotificationCategory.MemberInvitations, enabled: true },
        { category: NotificationCategory.TenantPlans, enabled: false },
      ],
    });
    expect(onSaved).toHaveBeenCalledOnce();
    await expectVisibleToast("Notification settings saved.");
  });

  it("NotificationPreferences_SaveWithTheMainSwitchOff_KeepsEachCategorysChoice", async () => {
    renderForm({
      values: {
        enabled: true,
        categories: [{ category: NotificationCategory.Access, enabled: true }],
      },
    });

    await userEvent.click(main());
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(save).toHaveBeenCalledExactlyOnceWith({
      enabled: false,
      categories: [{ category: NotificationCategory.Access, enabled: true }],
    });
  });

  it("NotificationPreferences_SwitchPressed_SavesNothingBeforeSave", async () => {
    renderForm();

    await userEvent.click(category("Your access"));

    expect(save).not.toHaveBeenCalled();
  });

  it("NotificationPreferences_SaveFails_GivesTheFailureBack", async () => {
    const failure = new Error("refused");
    save.mockRejectedValueOnce(failure);
    renderForm();

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(onFailed).toHaveBeenCalledWith(failure);
    expect(onSaved).not.toHaveBeenCalled();
  });

  it("NotificationPreferences_Arabic_NamesTheCategoriesInArabic", () => {
    renderForm({}, "ar");

    expect(screen.getByRole("switch", { name: "وصولك" })).toBeInTheDocument();
  });
});
