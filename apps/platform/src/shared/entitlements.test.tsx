import { LimitKey } from "@repo/api-client";
import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { messages } from "@/i18n/messages";
import { max } from "../../test/plans";
import { renderWithProviders } from "../../test/render";
import { useEntitlementText } from "./entitlements";

/** The Activity log's period of each number, as a value shows it: one item per number. */
function Periods({ days }: { days: number[] }) {
  const text = useEntitlementText();
  return (
    <ul>
      {days.map((count) => (
        <li key={count}>
          {text.label(false, max(count), LimitKey.AuditRetentionDays)}
        </li>
      ))}
    </ul>
  );
}

const periodsIn = (locale: keyof typeof messages, days: number[]) => {
  renderWithProviders(<Periods days={days} />, locale);
  return screen.getAllByRole("listitem").map((item) => item.textContent);
};

describe("useEntitlementText", () => {
  // CLDR's Arabic plural: few for 3 to 10, many for 11 to 99, other for 100 to 102.
  it("Label_ActivityLogInArabic_TakesThePluralOfEachNumber", () => {
    expect(periodsIn("ar", [3, 30, 90, 100, 365])).toEqual([
      "3 أيام",
      "30 يومًا",
      "90 يومًا",
      "100 يوم",
      "365 يومًا",
    ]);
  });

  it("Label_ActivityLogInEnglish_SaysItsDays", () => {
    expect(periodsIn("en", [1, 365])).toEqual(["1 day", "365 days"]);
  });

  it("Label_ActivityLogInTurkish_SaysItsDays", () => {
    expect(periodsIn("tr", [1, 365])).toEqual(["1 gün", "365 gün"]);
  });

  it("Label_ActivityLogWithNoValue_KeepsEveryEntry", () => {
    renderWithProviders(<NoValue />);

    expect(screen.getByText("Not set: no time limit")).toBeInTheDocument();
  });
});

function NoValue() {
  const text = useEntitlementText();
  return <p>{text.label(false, null, LimitKey.AuditRetentionDays)}</p>;
}
