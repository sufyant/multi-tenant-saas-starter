import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Avatar, initialsOf } from "./avatar";

describe("initialsOf", () => {
  it("InitialsOf_TwoWords_IsTheirFirstLetters", () => {
    expect(initialsOf({ name: "Ayşe Yılmaz" })).toBe("AY");
  });

  // The maintainer's decision of 2026-11-06: the first letters of the first and the last word, whatever lies between.
  it("InitialsOf_ThreeWords_IsTheFirstAndTheLastWordsFirstLetters", () => {
    expect(initialsOf({ name: "María  José García" })).toBe("MG");
  });

  // The Arabic article "ال" joins the word it defines, so the letter after it names the word (8.36).
  it("InitialsOf_ArabicWordsWithTheArticle_SkipTheArticle", () => {
    expect(initialsOf({ name: "عبد الرحمن الهاشمي" })).toBe("عه");
    expect(initialsOf({ name: "فاطمة الزهراء القحطاني" })).toBe("فق");
  });

  it("InitialsOf_LatinWordsThatStartWithAl_KeepTheirFirstLetters", () => {
    expect(initialsOf({ name: "Alia Al Hashimi" })).toBe("AH");
    expect(initialsOf({ name: "Alan Alvarez" })).toBe("AA");
  });

  it("InitialsOf_OneWord_IsItsFirstLetter", () => {
    expect(initialsOf({ name: "Ayşe" })).toBe("A");
  });

  it("InitialsOf_NoName_IsTheEmailsFirstLetterInCapitals", () => {
    expect(
      initialsOf({
        name: null,
        email: "ayse@northwind.example",
      }),
    ).toBe("A");
  });

  it("InitialsOf_DeletedUser_IsEmpty", () => {
    expect(initialsOf({ name: null })).toBe("");
  });
});

describe("Avatar", () => {
  it("Avatar_Person_ShowsTheInitialsHiddenFromScreenReaders", () => {
    const { container } = render(<Avatar person={{ name: "Ayşe Yılmaz" }} />);
    const avatar = container.querySelector('[data-slot="avatar"]');

    expect(avatar).toHaveTextContent("AY");
    expect(avatar).toHaveAttribute("aria-hidden", "true");
  });

  // jsdom draws nothing, so the look is the classes of the tokens; Playwright reads the top bar's drawn avatar.
  it("Avatar_InAList_IsTheSmallTintedCircle", () => {
    const { container } = render(<Avatar person={{ name: "Ayşe Yılmaz" }} />);

    expect(container.querySelector('[data-slot="avatar"]')).toHaveClass(
      "size-7",
      "rounded-full",
    );
    expect(
      container.querySelector('[data-slot="avatar-fallback"]'),
    ).toHaveClass("bg-role-owner-subtle", "text-role-owner-text");
  });

  it("Avatar_Size_TakesTheClass", () => {
    const { container } = render(
      <Avatar person={{ name: "Ayşe Yılmaz" }} className="size-11" />,
    );

    expect(container.querySelector('[data-slot="avatar"]')).toHaveClass(
      "size-11",
    );
  });
});
