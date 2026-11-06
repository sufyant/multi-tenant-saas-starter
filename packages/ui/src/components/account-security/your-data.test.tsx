import { ErrorCode } from "@repo/api-client";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import axe from "axe-core";
import { NextIntlClientProvider } from "next-intl";
import { afterEach, describe, expect, it, vi } from "vitest";
import en from "../../../messages/en.json";
import { expectVisibleToast } from "../../../test/toast";
import { dismissToasts, Toaster } from "../toast";
import { YourData, type YourDataProps } from "./your-data";

const messages = { ...en, locale: { format: "en-IE" } };
const now = new Date("2026-11-06T10:00:00Z");

function renderCard(overrides: Partial<YourDataProps> = {}) {
  const props: YourDataProps = {
    state: { kind: "none" },
    email: "ayse@northwind.example",
    request: vi.fn(async () => {}),
    download: vi.fn(async () => ({ url: "https://files.example/export.zip" })),
    readFailure: () => ({
      problem: { code: ErrorCode.ServerError, reference: "4bf92f35" },
      fields: null,
      saved: false,
    }),
    timeZone: "Europe/Dublin",
    now,
    ...overrides,
  };
  render(
    <NextIntlClientProvider
      locale="en"
      messages={messages}
      timeZone="Europe/Dublin"
    >
      <YourData {...props} />
      <Toaster dir="ltr" closeLabel="Close" regionLabel="Notifications" />
    </NextIntlClientProvider>,
  );
  return props;
}

afterEach(() => dismissToasts());

describe("YourData", () => {
  it("YourData_NoExport_AsksFirstThenStartsOneAndSaysSo", async () => {
    const props = renderCard();

    await userEvent.click(
      screen.getByRole("button", { name: "Export my data" }),
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "Export my data",
    });
    expect(dialog).toHaveTextContent(
      "We email ayse@northwind.example a link when it is ready.",
    );
    await userEvent.click(
      within(dialog).getByRole("button", { name: "Request export" }),
    );

    expect(props.request).toHaveBeenCalledWith(expect.any(String));
    await expectVisibleToast("Export started");
  });

  it("YourData_Preparing_SaysSoWithoutAnAction", () => {
    renderCard({ state: { kind: "inProgress" } });

    const card = screen.getByRole("region", { name: "Your data" });
    expect(within(card).getByText("Preparing")).toBeInTheDocument();
    expect(within(card).queryByRole("button")).toBeNull();
  });

  it("YourData_Ready_SaysUntilWhenAndDownloadsTheLink", async () => {
    const assign = vi.fn();
    vi.spyOn(window, "location", "get").mockReturnValue({
      ...window.location,
      assign,
    });
    const props = renderCard({
      state: {
        kind: "available",
        id: "exp_1",
        expiresAt: "2026-11-08T12:00:00Z",
      },
    });

    expect(screen.getByText("Ready")).toBeInTheDocument();
    expect(
      screen.getByText(/download until 8 Nov 2026\. Then we delete the file\./),
    ).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Download" }));

    expect(props.download).toHaveBeenCalledWith("exp_1");
    expect(assign).toHaveBeenCalledWith("https://files.example/export.zip");
    vi.restoreAllMocks();
  });

  it("YourData_Ready_OffersToExportAgain", () => {
    renderCard({
      state: {
        kind: "available",
        id: "exp_1",
        expiresAt: "2026-11-08T12:00:00Z",
      },
    });

    expect(
      screen.getByRole("button", { name: "Export again" }),
    ).toBeInTheDocument();
  });

  it("YourData_FileDeleted_SaysWhenAndOffersANewExport", () => {
    renderCard({
      state: { kind: "fileDeleted", expiresAt: "2026-11-08T12:00:00Z" },
    });

    expect(
      screen.getByText("Your last file was deleted on 8 Nov 2026."),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Export my data" }),
    ).toBeInTheDocument();
  });

  it("YourData_Unfinished_SaysSoAndOffersANewExport", () => {
    renderCard({ state: { kind: "unfinished" } });

    expect(
      screen.getByText(
        "The last export did not finish. Export your data again.",
      ),
    ).toBeInTheDocument();
  });

  it("YourData_RequestRefused_SaysWhyInTheDialog", async () => {
    renderCard({
      request: vi.fn().mockRejectedValue(new Error("refused")),
      readFailure: () => ({
        problem: {
          code: ErrorCode.DataExportInProgress,
          reference: "4bf92f35",
        },
        fields: null,
        saved: true,
      }),
    });

    await userEvent.click(
      screen.getByRole("button", { name: "Export my data" }),
    );
    await userEvent.click(
      await screen.findByRole("button", { name: "Request export" }),
    );

    expect(
      await screen.findByText(/An export of your data is being prepared\./),
    ).toBeInTheDocument();
  });

  it("YourData_ReadFailed_OffersToTryAgain", async () => {
    const onRetry = vi.fn();
    renderCard({ state: { kind: "error", onRetry } });

    await userEvent.click(screen.getByRole("button", { name: "Try again" }));

    expect(onRetry).toHaveBeenCalledOnce();
  });

  it("YourData_CardAndDialog_HaveNoAccessibilityViolation", async () => {
    renderCard();
    const violations = async (within: Element) =>
      (
        await axe.run(
          { include: [within], exclude: [["[data-base-ui-focus-guard]"]] },
          {
            rules: {
              "color-contrast": { enabled: false },
              region: { enabled: false },
            },
          },
        )
      ).violations.map((violation) => violation.id);

    expect(
      await violations(screen.getByRole("region", { name: "Your data" })),
    ).toEqual([]);
    await userEvent.click(
      screen.getByRole("button", { name: "Export my data" }),
    );
    expect(
      await violations(
        await screen.findByRole("alertdialog", { name: "Export my data" }),
      ),
    ).toEqual([]);
  });
});
