import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { NoticeScreen } from "./notice-screen";

describe("NoticeScreen", () => {
  // Frames 2.13 and 1.19: the title and the sentence start where the line starts, in a card of their own width.
  it("NoticeScreen_TitleAndText_ShowsThemStartAlignedInTheCard", () => {
    render(
      <NoticeScreen
        title="Northwind Traders is suspended"
        text="You cannot open this organisation right now."
        actions={<button type="button">Sign out</button>}
      />,
    );

    const heading = screen.getByRole("heading", {
      level: 1,
      name: "Northwind Traders is suspended",
    });
    expect(heading).not.toHaveClass("text-center");
    const card = heading.parentElement;
    expect(card).toHaveClass("max-w-notice-card-max", "shadow-md");
    expect(card).toContainElement(
      screen.getByRole("button", { name: "Sign out" }),
    );
  });

  // Frame 1.19: the account button sits at the top end of the page, outside the card.
  it("NoticeScreen_Corner_ShowsItOutsideTheCard", () => {
    render(
      <NoticeScreen
        title="No access yet"
        text="You’re not in an organisation yet."
        corner={<button type="button">Lena Fischer</button>}
      />,
    );

    const card = screen.getByRole("heading", {
      name: "No access yet",
    }).parentElement;
    expect(card).not.toContainElement(
      screen.getByRole("button", { name: "Lena Fischer" }),
    );
  });

  it("NoticeScreen_NoActions_DrawsNoActionRow", () => {
    render(<NoticeScreen title="No access yet" text="Ask an admin." />);

    expect(
      screen.getByRole("heading", { name: "No access yet" }).parentElement
        ?.children,
    ).toHaveLength(2);
  });
});
