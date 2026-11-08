import { act, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { showToast, Toaster } from "./toast";

describe("Toast", () => {
  it("ShowToast_Success_ShowsItsTitle", async () => {
    render(
      <Toaster dir="ltr" closeLabel="Close" regionLabel="Notifications" />,
    );

    act(() =>
      showToast({
        title: "Invitation sent",
        description: "ali@northwind.io has 7 days to accept.",
      }),
    );

    expect(await screen.findByText("Invitation sent")).toBeInTheDocument();
  });

  it("ShowToast_InRtl_ShowsItsTitle", async () => {
    render(<Toaster dir="rtl" closeLabel="إغلاق" regionLabel="الإشعارات" />);

    act(() => showToast({ title: "تم إرسال الدعوة" }));

    expect(await screen.findByText("تم إرسال الدعوة")).toBeInTheDocument();
  });

  // The Toast component draws no close button (45:523): a success leaves by itself.
  it("ShowToast_Success_HasNoCloseButton", async () => {
    render(
      <Toaster dir="ltr" closeLabel="Close" regionLabel="Notifications" />,
    );

    act(() => showToast({ title: "Member removed" }));

    await screen.findByText("Member removed");
    expect(
      screen.queryByRole("button", { name: "Close" }),
    ).not.toBeInTheDocument();
  });

  // A failure stays until the person closes it, so it keeps its close button (WCAG 2.2, 2.2.1; the maintainer's decision of 2026-11-08).
  it("ShowToast_Destructive_HasACloseButton", async () => {
    render(
      <Toaster dir="ltr" closeLabel="Close" regionLabel="Notifications" />,
    );

    act(() => showToast({ title: "Not done", tone: "destructive" }));

    await screen.findByText("Not done");
    expect(
      await screen.findByRole("button", { name: "Close" }),
    ).toBeInTheDocument();
  });
});
