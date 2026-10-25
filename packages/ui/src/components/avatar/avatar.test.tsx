import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Avatar, initialsOf } from "./avatar";

describe("initialsOf", () => {
  it("InitialsOf_TwoNames_IsTheirFirstLetters", () => {
    expect(initialsOf({ firstName: "Ayşe", lastName: "Yılmaz" })).toBe("AY");
  });

  it("InitialsOf_OneName_IsItsFirstLetter", () => {
    expect(initialsOf({ firstName: "Ayşe", lastName: null })).toBe("A");
  });

  it("InitialsOf_NoNames_IsTheEmailsFirstLetterInCapitals", () => {
    expect(
      initialsOf({
        firstName: null,
        lastName: null,
        email: "ayse@northwind.example",
      }),
    ).toBe("A");
  });

  it("InitialsOf_DeletedUser_IsEmpty", () => {
    expect(initialsOf({ firstName: null, lastName: null })).toBe("");
  });
});

describe("Avatar", () => {
  it("Avatar_Person_ShowsTheInitialsHiddenFromScreenReaders", () => {
    const { container } = render(
      <Avatar person={{ firstName: "Ayşe", lastName: "Yılmaz" }} />,
    );
    const avatar = container.querySelector('[data-slot="avatar"]');

    expect(avatar).toHaveTextContent("AY");
    expect(avatar).toHaveAttribute("aria-hidden", "true");
  });

  // jsdom draws nothing, so the look is the classes of the tokens; Playwright reads the top bar's drawn avatar.
  it("Avatar_InAList_IsTheSmallFilledCircle", () => {
    const { container } = render(
      <Avatar person={{ firstName: "Ayşe", lastName: "Yılmaz" }} />,
    );

    expect(container.querySelector('[data-slot="avatar"]')).toHaveClass(
      "size-7",
      "rounded-full",
    );
    expect(
      container.querySelector('[data-slot="avatar-fallback"]'),
    ).toHaveClass("bg-neutral-text", "text-background", "font-semibold");
  });

  it("Avatar_Size_TakesTheClass", () => {
    const { container } = render(
      <Avatar
        person={{ firstName: "Ayşe", lastName: "Yılmaz" }}
        className="size-11"
      />,
    );

    expect(container.querySelector('[data-slot="avatar"]')).toHaveClass(
      "size-11",
    );
  });
});
