import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { Dialog } from "./dialog";

describe("Dialog", () => {
  it("Dialog_ConfirmText_KeepsTheButtonDisabledUntilItMatches", async () => {
    render(
      <Dialog
        defaultOpen
        tone="destructive"
        title="Close Northwind MRO?"
        actionLabel="Close tenant"
        cancelLabel="Cancel"
        confirmText="northwind"
        confirmLabel="Type northwind to confirm"
      />,
    );
    const confirm = await screen.findByRole("button", { name: "Close tenant" });
    const field = screen.getByRole("textbox", {
      name: "Type northwind to confirm",
    });

    await userEvent.type(field, "northwin");
    expect(confirm).toBeDisabled();

    await userEvent.type(field, "d");
    expect(confirm).toBeEnabled();
  });

  it("Dialog_Confirm_CallsItsAction", async () => {
    const onConfirm = vi.fn();
    render(
      <Dialog
        defaultOpen
        title="Remove Ayşe Yılmaz?"
        actionLabel="Remove member"
        cancelLabel="Cancel"
        onConfirm={onConfirm}
      />,
    );

    await userEvent.click(
      await screen.findByRole("button", { name: "Remove member" }),
    );

    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it("Dialog_InRtl_NamesTheObject", async () => {
    render(
      <div dir="rtl">
        <Dialog
          defaultOpen
          title="إزالة عائشة يلماز؟"
          actionLabel="إزالة العضو"
          cancelLabel="إلغاء"
        />
      </div>,
    );

    expect(
      await screen.findByRole("alertdialog", { name: "إزالة عائشة يلماز؟" }),
    ).toBeInTheDocument();
  });
});
