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

  it("CommandDialog_Open_Is520WideAnd90FromTheTop", () => {
    render(
      <CommandDialog open title="Go to">
        <Command>
          <CommandInput placeholder="Search pages" />
        </Command>
      </CommandDialog>,
    );

    const dialog = screen.getByRole("dialog");
    expect(dialog).toHaveClass("top-22.5", "tablet:max-w-130");
    expect(dialog).not.toHaveClass("tablet:max-w-sm");
  });
});
