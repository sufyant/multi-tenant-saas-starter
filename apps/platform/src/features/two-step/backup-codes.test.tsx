import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { renderWithProviders } from "../../../test/render";
import { BackupCodes } from "./backup-codes";

const codes = Array.from({ length: 10 }, (_, index) => `code${index}`);

describe("BackupCodes", () => {
  it("BackupCodes_NotConfirmed_KeepsContinueDisabled", () => {
    renderWithProviders(<BackupCodes codes={codes} onContinue={vi.fn()} />);

    expect(
      screen.getByRole("button", { name: "Continue to the platform" }),
    ).toBeDisabled();
  });

  it("BackupCodes_Confirmed_LetsThePersonContinue", async () => {
    const user = userEvent.setup();
    const onContinue = vi.fn();
    renderWithProviders(<BackupCodes codes={codes} onContinue={onContinue} />);

    await user.click(
      screen.getByRole("checkbox", {
        name: "I saved these codes. I understand I cannot see them again, only make new ones.",
      }),
    );
    await user.click(
      screen.getByRole("button", { name: "Continue to the platform" }),
    );

    expect(onContinue).toHaveBeenCalledTimes(1);
  });

  it("BackupCodes_Download_BuildsTheFileInTheBrowser", async () => {
    const user = userEvent.setup();
    const createObjectURL = vi.fn((_: Blob) => "blob:codes");
    URL.createObjectURL = createObjectURL;
    URL.revokeObjectURL = vi.fn();
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, "click")
      .mockImplementation(() => {});
    renderWithProviders(<BackupCodes codes={codes} onContinue={vi.fn()} />);

    await user.click(screen.getByRole("button", { name: "Download as text" }));

    expect(click).toHaveBeenCalledTimes(1);
    expect(await createObjectURL.mock.calls[0]?.[0].text()).toBe(
      `${codes.join("\n")}\n`,
    );
  });

  it("BackupCodes_Copy_WritesEveryCodeToTheClipboard", async () => {
    const user = userEvent.setup();
    renderWithProviders(<BackupCodes codes={codes} onContinue={vi.fn()} />);

    await user.click(screen.getByRole("button", { name: "Copy codes" }));

    expect(await navigator.clipboard.readText()).toBe(`${codes.join("\n")}\n`);
  });
});
