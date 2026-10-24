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
        title="Close Northwind Traders?"
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

  it("Dialog_Pending_DisablesTheActionAndSaysItIsWorking", async () => {
    const onConfirm = vi.fn();
    render(
      <Dialog
        defaultOpen
        pending
        title="Remove Ayşe Yılmaz?"
        actionLabel="Remove member"
        cancelLabel="Cancel"
        onConfirm={onConfirm}
      />,
    );

    const action = await screen.findByRole("button", { name: "Remove member" });
    expect(action).toBeDisabled();
    expect(action).toHaveAttribute("aria-busy", "true");
  });

  it("Dialog_SecondaryAction_ShowsTheActionAsASecondaryButton", async () => {
    render(
      <Dialog
        defaultOpen
        action="secondary"
        title="Make Mehmet Kaya a system admin?"
        actionLabel="Make system admin"
        cancelLabel="Cancel"
      />,
    );

    const action = await screen.findByRole("button", {
      name: "Make system admin",
    });
    expect(action).toHaveClass("bg-card");
    expect(action).not.toHaveClass("bg-primary");
  });

  it("Dialog_ActionDisabled_DisablesTheAction", async () => {
    render(
      <Dialog
        defaultOpen
        actionDisabled
        title="Add system admin"
        actionLabel="Continue"
        cancelLabel="Cancel"
      />,
    );

    expect(
      await screen.findByRole("button", { name: "Continue" }),
    ).toBeDisabled();
  });

  it("Dialog_Back_GoesBackAndStaysOpen", async () => {
    const onBack = vi.fn();
    render(
      <Dialog
        defaultOpen
        title="Make Mehmet Kaya a system admin?"
        actionLabel="Make system admin"
        cancelLabel="Cancel"
        back={{ label: "Back", onBack }}
      />,
    );

    await userEvent.click(await screen.findByRole("button", { name: "Back" }));

    expect(onBack).toHaveBeenCalledOnce();
    expect(screen.queryByRole("button", { name: "Cancel" })).toBeNull();
    expect(
      screen.getByRole("alertdialog", {
        name: "Make Mehmet Kaya a system admin?",
      }),
    ).toBeInTheDocument();
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
