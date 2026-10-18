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
});
