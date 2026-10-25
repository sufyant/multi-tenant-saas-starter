import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "./tooltip";

describe("Tooltip", () => {
  it("TooltipContent_Open_SitsOnThePopoverLayer", () => {
    render(
      <TooltipProvider>
        <Tooltip open>
          <TooltipTrigger>Updated 10:55</TooltipTrigger>
          <TooltipContent>25 Oct 2026, 10:55</TooltipContent>
        </Tooltip>
      </TooltipProvider>,
    );

    // The positioner is the element in the portal; the popup sits inside it.
    expect(screen.getByText("25 Oct 2026, 10:55").parentElement).toHaveClass(
      "z-popover",
    );
  });
});
