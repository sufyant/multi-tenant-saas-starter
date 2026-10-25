import { act, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { InlineSkeleton, Skeleton, skeletonDelayMs } from "./skeleton";

describe("Skeleton", () => {
  it("Skeleton_FirstLoad_IsABusyStatusWithItsLabel", () => {
    render(<Skeleton rows={3} label="Loading members" />);

    expect(
      screen.getByRole("status", { name: "Loading members" }),
    ).toHaveAttribute("aria-busy", "true");
  });

  it("Skeleton_InRtl_Renders", () => {
    render(
      <div dir="rtl">
        <Skeleton label="جارٍ التحميل" />
      </div>,
    );

    expect(
      screen.getByRole("status", { name: "جارٍ التحميل" }),
    ).toBeInTheDocument();
  });

  describe("delayed", () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });
    afterEach(() => {
      vi.useRealTimers();
    });

    it("Skeleton_DelayedBeforeTheDelay_ShowsNothing", () => {
      render(<Skeleton delayed label="Loading" />);

      act(() => {
        vi.advanceTimersByTime(skeletonDelayMs - 1);
      });

      expect(screen.queryByRole("status")).not.toBeInTheDocument();
    });

    it("Skeleton_DelayedAfterTheDelay_ShowsTheRows", () => {
      render(<Skeleton delayed label="Loading" />);

      act(() => {
        vi.advanceTimersByTime(skeletonDelayMs);
      });

      expect(
        screen.getByRole("status", { name: "Loading" }),
      ).toBeInTheDocument();
    });

    it("InlineSkeleton_BeforeAndAfterTheDelay_ShowsOnlyAfterIt", () => {
      const { container } = render(<InlineSkeleton />);

      const before = container.querySelectorAll("[data-slot='skeleton']");
      act(() => {
        vi.advanceTimersByTime(skeletonDelayMs);
      });

      expect(before).toHaveLength(0);
      expect(container.querySelector("[data-slot='skeleton']")).toHaveAttribute(
        "aria-hidden",
        "true",
      );
    });
  });
});
