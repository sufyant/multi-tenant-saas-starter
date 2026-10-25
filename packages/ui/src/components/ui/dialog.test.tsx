import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Dialog, DialogContent, DialogTitle } from "./dialog";

describe("Dialog", () => {
  it("DialogContent_Open_PutsTheBackdropAndThePopupOnTheDialogLayer", () => {
    render(
      <Dialog open>
        <DialogContent>
          <DialogTitle>Rename organisation</DialogTitle>
        </DialogContent>
      </Dialog>,
    );

    expect(screen.getByRole("dialog")).toHaveClass("z-dialog");
    // The backdrop has no role.
    expect(document.querySelector('[data-slot="dialog-overlay"]')).toHaveClass(
      "z-dialog",
    );
  });
});
