import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Sheet, SheetContent, SheetTitle } from "./sheet";

describe("Sheet", () => {
  it("SheetContent_Open_PutsTheBackdropAndThePopupOnTheDrawerLayer", () => {
    render(
      <Sheet open>
        <SheetContent>
          <SheetTitle>Navigation</SheetTitle>
        </SheetContent>
      </Sheet>,
    );

    expect(screen.getByRole("dialog")).toHaveClass("z-drawer");
    // The backdrop has no role.
    expect(document.querySelector('[data-slot="sheet-overlay"]')).toHaveClass(
      "z-drawer",
    );
  });
});
