import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { KeyIcon } from "../icon";
import { Tag } from "./tag";

describe("Tag", () => {
  it("Tag_AfterAName_ShowsItsWord", () => {
    render(<Tag>System admin</Tag>);

    expect(screen.getByText("System admin")).toBeInTheDocument();
  });

  it("Tag_InRtl_ShowsItsWord", () => {
    render(
      <div dir="rtl">
        <Tag>مسؤول نظام</Tag>
      </div>,
    );

    expect(screen.getByText("مسؤول نظام")).toBeInTheDocument();
  });

  // jsdom draws nothing, so the look is the classes of the tokens (the Tag README); Playwright reads the drawn boxes.
  it("Tag_KindOfRecord_IsTheQuietBadge", () => {
    render(<Tag>Built in</Tag>);

    const tag = screen.getByText("Built in");
    expect(tag).toHaveClass(
      "h-5",
      "rounded-full",
      "bg-neutral/16",
      "text-neutral-text",
      "text-small",
      "font-medium",
    );
    expect(tag.className).not.toMatch(/\bborder\b/);
  });

  it("Tag_ViewersOwnRow_IsMutedTextWithoutABox", () => {
    render(<Tag variant="text">(you)</Tag>);

    const tag = screen.getByText("(you)");
    expect(tag).toHaveClass("text-muted-foreground", "font-normal");
    expect(tag.className).not.toMatch(/\b(bg-|border|rounded)/);
  });

  it("Tag_WithAnIcon_DrawsItBeforeTheText", () => {
    render(<Tag icon={KeyIcon}>Root</Tag>);

    const tag = screen.getByText("Root");
    expect(tag.firstElementChild?.tagName).toBe("svg");
    expect(tag.firstElementChild).toHaveAttribute("aria-hidden", "true");
    expect(tag).toHaveClass("bg-neutral/16");
  });
});
