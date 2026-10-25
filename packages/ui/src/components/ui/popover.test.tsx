import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Popover, PopoverContent, PopoverTrigger } from "./popover";

describe("Popover", () => {
  it("PopoverContent_Open_SitsOnThePopoverLayer", () => {
    render(
      <Popover open>
        <PopoverTrigger>Filters</PopoverTrigger>
        <PopoverContent>Role</PopoverContent>
      </Popover>,
    );

    // The positioner is the element in the portal; the popup sits inside it.
    expect(screen.getByText("Role").parentElement).toHaveClass("z-popover");
  });
});
