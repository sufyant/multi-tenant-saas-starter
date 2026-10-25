import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import {
  Combobox,
  ComboboxContent,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
} from "./combobox";

describe("Combobox", () => {
  it("ComboboxContent_Open_SitsOnThePopoverLayer", () => {
    render(
      <Combobox open items={["Admin"]}>
        <ComboboxInput aria-label="Role" />
        <ComboboxContent>
          <ComboboxList>
            {(item: string) => (
              <ComboboxItem key={item} value={item}>
                {item}
              </ComboboxItem>
            )}
          </ComboboxList>
        </ComboboxContent>
      </Combobox>,
    );

    // The positioner is the element in the portal; the popup sits inside it.
    expect(
      screen.getByRole("option", { name: "Admin" }).closest(".isolate"),
    ).toHaveClass("z-popover");
  });
});
