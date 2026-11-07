import { render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { BackupCodeList, TotpFields } from "./totp-fields";
import type { Totp } from "./use-totp-enrolment";

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
  // Platform frame 1.04: in the card before sign-in, Download and Copy sit in the middle.
  it("BackupCodeList_Centred_PutsTheButtonsInTheMiddle", () => {
    render(<BackupCodeList codes={["4f7k-2m9q"]} labels={labels} centred />);

    expect(
      screen.getByRole("button", { name: "Download" }).parentElement,
    ).toHaveClass("justify-center");
  });
});

describe("TotpFields", () => {
  // Platform frame 1.03: the QR code, the key on one line with its copy button, and the six boxes, all in the middle of the card.
  it("TotpFields_Centred_ShowsTheKeyInGroupsOfFourAndTheCode", () => {
    render(
      <TotpFields
        centred
        totp={{ secret: "JBSWY3DPEHPK3PXP", uri: "otpauth://totp/x" } as Totp}
        code=""
        onCodeChange={vi.fn()}
        labels={{
          scan: "Scan the QR code",
          qr: "QR code for your authenticator app",
          cannotScan: "Key:",
          copyKey: "Copy key",
          copied: "Copied",
          enterCode: "Code from the app",
        }}
      />,
    );

    expect(screen.getByText("JBSW Y3DP EHPK 3PXP")).toHaveAttribute(
      "dir",
      "ltr",
    );
    expect(
      screen.getByRole("button", { name: "Copy key" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("textbox", { name: "Code from the app" }),
    ).toBeInTheDocument();
  });
});
