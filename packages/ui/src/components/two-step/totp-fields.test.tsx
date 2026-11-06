import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { BackupCodeList } from "./totp-fields";

const labels = {
  list: "Backup codes",
  copy: "Copy",
  download: "Download",
  fileName: "backup-codes.txt",
};

describe("BackupCodeList", () => {
  // The order of 6.26: Download, then Copy.
  it("BackupCodeList_Shown_ListsTheCodesLeftToRightThenDownloadAndCopy", () => {
    render(
      <BackupCodeList codes={["4f7k-2m9q", "b2hc-q7wv"]} labels={labels} />,
    );

    const list = screen.getByRole("list", { name: "Backup codes" });
    expect(list).toHaveAttribute("dir", "ltr");
    expect(
      within(list)
        .getAllByRole("listitem")
        .map((item) => item.textContent),
    ).toEqual(["4f7k-2m9q", "b2hc-q7wv"]);
    expect(
      screen.getAllByRole("button").map((button) => button.textContent),
    ).toEqual(["Download", "Copy"]);
  });
});
