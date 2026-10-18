import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
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
