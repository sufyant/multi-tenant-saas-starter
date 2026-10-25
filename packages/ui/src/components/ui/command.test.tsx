import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Command, CommandDialog, CommandInput } from "./command";

describe("CommandDialog", () => {
  it("CommandDialog_Open_SitsOnTheDialogLayer", () => {
    render(
      <CommandDialog open title="Go to">
        <Command>
          <CommandInput placeholder="Search pages" />
        </Command>
      </CommandDialog>,
    );

    expect(screen.getByRole("dialog")).toHaveClass("z-dialog");
  });
});
