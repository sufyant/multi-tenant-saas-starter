import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { BellIcon } from "../icon";
import { EmptyState } from "./empty-state";

describe("EmptyState", () => {
  it("EmptyState_Empty_NamesTheSpace", () => {
    render(
      <EmptyState
        title="No pending invitations"
        description="Invite people by email."
      />,
    );

    expect(
      screen.getByRole("heading", { name: "No pending invitations" }),
    ).toBeInTheDocument();
  });

  it("EmptyState_Error_IsAnAlertWithTheReference", () => {
    render(
      <EmptyState
        kind="error"
        title="Couldn’t load members"
        code="Ref 7f3a9c · 13:32 UTC"
      />,
    );

    expect(screen.getByRole("alert")).toHaveTextContent(
      "Ref 7f3a9c · 13:32 UTC",
    );
  });

  it("EmptyState_Page_SitsBelowTheTopBarFromBpTablet", () => {
    render(<EmptyState size="page" title="Page not found" />);

    const state = screen.getByRole("heading", {
      name: "Page not found",
    }).parentElement;
    expect(state).toHaveClass("py-10", "tablet:pt-30");
  });

  // The bell's list (3.03, 3.15): a small title and an icon without its tile, lower on a phone's full screen.
  it("EmptyState_Compact_HasASmallTitleAndNoTile", () => {
    const { container } = render(
      <EmptyState size="compact" icon={BellIcon} title="No notifications" />,
    );

    const title = screen.getByRole("heading", { name: "No notifications" });
    expect(title).toHaveClass("text-small", "font-medium");
    expect(title.parentElement).toHaveClass("py-7", "max-tablet:pt-40");
    expect(container.querySelector(".bg-muted")).toBeNull();
  });

  it("EmptyState_InRtl_Renders", () => {
    render(
      <div dir="rtl">
        <EmptyState title="لا توجد دعوات" />
      </div>,
    );

    expect(
      screen.getByRole("heading", { name: "لا توجد دعوات" }),
    ).toBeInTheDocument();
  });
});
