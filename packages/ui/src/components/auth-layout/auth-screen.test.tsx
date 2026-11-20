import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { AuthHeading, AuthScreen } from "./auth-screen";

describe("AuthScreen", () => {
  it("AuthScreen_Web_NamesTheProductByItsMarkAndShowsTheTitleAndText", () => {
    render(
      <AuthScreen brand="Starter">
        <AuthHeading
          title="Sign in to your account"
          text="Use your work email."
        />
      </AuthScreen>,
    );

    expect(screen.getByRole("img", { name: "Starter" })).toHaveTextContent("S");
    expect(
      screen.getByRole("heading", {
        level: 1,
        name: "Sign in to your account",
      }),
    ).toBeInTheDocument();
    expect(screen.getByText("Use your work email.")).toBeInTheDocument();
    expect(screen.queryByText("Platform")).not.toBeInTheDocument();
  });

  it("AuthScreen_Platform_ShowsThePlatformLabelUnderTheMark", () => {
    render(<AuthScreen brand="Starter" platformLabel="Platform" />);

    expect(screen.getByText("Platform")).toBeInTheDocument();
  });

  it("AuthScreen_Below_ShowsItOutsideTheCard", () => {
    render(
      <AuthScreen
        brand="Starter"
        below={<p>Signed in as deniz@platform.example</p>}
      >
        <p>The codes</p>
      </AuthScreen>,
    );

    const card = screen.getByRole("main").firstElementChild;
    expect(card).toContainElement(screen.getByText("The codes"));
    expect(card).not.toContainElement(
      screen.getByText("Signed in as deniz@platform.example"),
    );
  });

  // 1.28, 1.30: a step with a form of its own starts its title and text at the card's start.
  it("AuthHeading_AlignStart_StartsTheTitleAndText", () => {
    render(
      <AuthHeading
        align="start"
        title="Create your organisation"
        text="You become its Owner."
      />,
    );

    expect(
      screen.getByRole("heading", { name: "Create your organisation" }),
    ).toHaveClass("text-start");
    expect(screen.getByText("You become its Owner.")).toHaveClass("text-start");
  });
});
