import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Truncate } from "./truncate";

const name = "Maximiliane Alexandra von Hohenzollern-Sigmaringen";

/** jsdom lays nothing out: the test gives the text the widths a browser would measure. */
function widths(scroll: number, client: number) {
  vi.spyOn(HTMLElement.prototype, "scrollWidth", "get").mockReturnValue(scroll);
  vi.spyOn(HTMLElement.prototype, "clientWidth", "get").mockReturnValue(client);
}

describe("Truncate", () => {
  afterEach(() => vi.restoreAllMocks());

  it("Truncate_TextFits_HasNoTooltipAndNoTabStop", () => {
    widths(100, 100);
    render(<Truncate>Members</Truncate>);

    expect(screen.getByText("Members")).toHaveClass("truncate");
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    expect(document.querySelector("[tabindex]")).toBeNull();
  });

  it("Truncate_TextCut_ShowsTheFullTextOnKeyboardFocus", async () => {
    widths(400, 120);
    render(<Truncate>{name}</Truncate>);

    await userEvent.tab();

    // The text itself and the tooltip's copy of it.
    expect(await screen.findAllByText(name)).toHaveLength(2);
  });

  // A link holds the text: the link opens the tooltip, and no second tab stop sits inside it.
  it("Truncate_CutInsideALink_ShowsTheTooltipFromTheLinkWithNoSecondStop", async () => {
    widths(400, 120);
    render(<Truncate render={<a href="/settings" />}>{name}</Truncate>);

    await userEvent.tab();

    const link = screen.getByRole("link");
    expect(link).toHaveFocus();
    expect(link.querySelector("[tabindex]")).toBeNull();
    expect(await screen.findAllByText(name)).toHaveLength(2);
  });

  it("Truncate_FitsInsideALink_RendersTheLinkWithTheText", () => {
    widths(100, 100);
    render(<Truncate render={<a href="/settings" />}>Members</Truncate>);

    expect(screen.getByRole("link", { name: "Members" })).toHaveAttribute(
      "href",
      "/settings",
    );
  });
});
