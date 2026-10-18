import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Tag } from "./tag";

describe("Tag", () => {
  it("Tag_AfterAName_ShowsItsWord", () => {
    render(<Tag>You</Tag>);

    expect(screen.getByText("You")).toBeInTheDocument();
  });

  it("Tag_InRtl_ShowsItsWord", () => {
    render(
      <div dir="rtl">
        <Tag>أنت</Tag>
      </div>,
    );

    expect(screen.getByText("أنت")).toBeInTheDocument();
  });
});
