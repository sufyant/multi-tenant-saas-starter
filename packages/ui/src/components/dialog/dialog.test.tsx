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

  // The Dialogs on a phone pattern: a form you type into is a full-screen page with × and the title at the top.
  it("Dialog_FieldInside_IsAFormWithACloseButtonAtTheTop", async () => {
    const onOpenChange = vi.fn();
    render(
      <Dialog
        defaultOpen
        onOpenChange={onOpenChange}
        title="Invite member"
        actionLabel="Send invitation"
        cancelLabel="Cancel"
      >
        <label>
          Email
          <input type="email" />
        </label>
      </Dialog>,
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "Invite member",
    });

    expect(dialog).toHaveAttribute("data-kind", "form");
    // The × of the top bar comes first; the footer's Cancel shows only from bp-tablet.
    const [close] = screen.getAllByRole("button", { name: "Cancel" });
    expect(close).toHaveAttribute("data-slot", "dialog-close");
    await userEvent.click(close as HTMLElement);
    expect(onOpenChange).toHaveBeenCalledWith(false, expect.anything());
  });

  it("Dialog_TypeToConfirm_IsAForm", async () => {
    render(
      <Dialog
        defaultOpen
        tone="destructive"
        title="Close Northwind Traders?"
        actionLabel="Close organisation"
        cancelLabel="Cancel"
        confirmText="northwind"
        confirmLabel="Type northwind to confirm"
      />,
    );

    expect(
      await screen.findByRole("alertdialog", {
        name: "Close Northwind Traders?",
      }),
    ).toHaveAttribute("data-kind", "form");
  });

  // A short choice or a confirmation is a bottom sheet with a drag handle, the page dimmed behind it.
  it("Dialog_NoField_IsASheetWithAHandle", async () => {
    render(
      <Dialog
        defaultOpen
        title="Remove Omar Haddad?"
        actionLabel="Remove member"
        cancelLabel="Cancel"
      />,
    );
    const dialog = await screen.findByRole("alertdialog", {
      name: "Remove Omar Haddad?",
    });

    expect(dialog).toHaveAttribute("data-kind", "sheet");
    expect(dialog.querySelector("[data-slot=sheet-handle]")).not.toBeNull();
    expect(screen.getAllByRole("button", { name: "Cancel" })).toHaveLength(1);
  });
});
