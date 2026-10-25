import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogTitle,
} from "./alert-dialog";

describe("AlertDialog", () => {
  it("AlertDialogContent_Open_PutsTheBackdropAndThePopupOnTheDialogLayer", () => {
    render(
      <AlertDialog open>
        <AlertDialogContent>
          <AlertDialogTitle>Remove member</AlertDialogTitle>
        </AlertDialogContent>
      </AlertDialog>,
    );

    expect(screen.getByRole("alertdialog")).toHaveClass("z-dialog");
    // The backdrop has no role.
    expect(
      document.querySelector('[data-slot="alert-dialog-overlay"]'),
    ).toHaveClass("z-dialog");
  });
});
