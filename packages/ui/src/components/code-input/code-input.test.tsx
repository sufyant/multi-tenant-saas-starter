import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { CodeInput } from "./code-input";

function Harness({
  onComplete,
  error,
}: {
  onComplete?: (code: string) => void;
  error?: string;
}) {
  const [value, setValue] = useState("");
  return (
    <CodeInput
      label="Code"
      value={value}
      onChange={setValue}
      onComplete={onComplete}
      error={error}
    />
  );
}

describe("CodeInput", () => {
  it("CodeInput_Paste_FillsEverySlotAndCompletes", async () => {
    const user = userEvent.setup();
    const onComplete = vi.fn();
    render(<Harness onComplete={onComplete} />);

    await user.click(screen.getByRole("textbox", { name: "Code" }));
    await user.paste("424242");

    expect(screen.getByRole("textbox", { name: "Code" })).toHaveValue("424242");
    expect(onComplete).toHaveBeenCalledWith("424242");
  });

  it("CodeInput_Backspace_DeletesTheLastDigit", async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.type(screen.getByRole("textbox", { name: "Code" }), "4242");
    await user.keyboard("{Backspace}");

    expect(screen.getByRole("textbox", { name: "Code" })).toHaveValue("424");
  });

  it("CodeInput_Letters_AreRefused", async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.type(screen.getByRole("textbox", { name: "Code" }), "4a2");

    expect(screen.getByRole("textbox", { name: "Code" })).toHaveValue("42");
  });

  it("CodeInput_InRtl_KeepsTheSlotsLeftToRight", () => {
    const { container } = render(
      <div dir="rtl">
        <Harness />
      </div>,
    );

    expect(
      container.querySelector("[data-slot='input-otp-group']"),
    ).toHaveAttribute("dir", "ltr");
  });

  it("CodeInput_ForTheBrowser_AsksForANumericOneTimeCode", () => {
    render(<Harness />);

    const input = screen.getByRole("textbox", { name: "Code" });
    expect(input).toHaveAttribute("inputmode", "numeric");
    expect(input).toHaveAttribute("autocomplete", "one-time-code");
  });

  it("CodeInput_Slots_AreSixSeparateBoxesFromTheTokens", () => {
    const { container } = render(<Harness />);

    const group = container.querySelector("[data-slot='input-otp-group']");
    const boxes = [
      ...container.querySelectorAll("[data-slot='input-otp-slot']"),
    ].map((box) => [...box.classList]);
    expect(group).toHaveClass("gap-2");
    expect(boxes).toEqual(
      Array.from({ length: 6 }, () =>
        expect.arrayContaining([
          "w-code-slot-width",
          "h-code-slot-height",
          "rounded-md",
          "border",
          "font-mono",
          "text-section-title",
        ]),
      ),
    );
    expect(boxes.flat()).not.toEqual(expect.arrayContaining(["border-y"]));
  });

  it("CodeInput_WithError_MarksEveryBox", () => {
    const { container } = render(<Harness error="That code is not right." />);

    const boxes = [
      ...container.querySelectorAll("[data-slot='input-otp-slot']"),
    ];
    expect(boxes).toHaveLength(6);
    expect(
      boxes.filter((box) => box.getAttribute("aria-invalid") === "true"),
    ).toHaveLength(6);
  });

  it("CodeInput_Paste_ShowsADigitInEachOfTheSixBoxes", async () => {
    const user = userEvent.setup();
    const { container } = render(<Harness />);

    await user.click(screen.getByRole("textbox", { name: "Code" }));
    await user.paste("424242");

    expect(
      [...container.querySelectorAll("[data-slot='input-otp-slot']")].map(
        (box) => box.textContent,
      ),
    ).toEqual(["4", "2", "4", "2", "4", "2"]);
  });

  it("CodeInput_WithError_IsInvalidAndAnnouncesIt", () => {
    render(<Harness error="That code is not right." />);

    const input = screen.getByRole("textbox", { name: "Code" });
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAccessibleDescription("That code is not right.");
    expect(screen.getByRole("alert")).toHaveTextContent(
      "That code is not right.",
    );
  });
});
