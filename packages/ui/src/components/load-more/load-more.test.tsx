import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { LoadMore, type LoadMoreProps } from "./load-more";

const renderLoadMore = (props: Partial<LoadMoreProps> = {}) =>
  render(
    <LoadMore
      hasMore
      loading={false}
      onMore={() => {}}
      label="Load more"
      loadingLabel="Loading more members"
      announcement={null}
      {...props}
    />,
  );

describe("LoadMore", () => {
  it("LoadMore_NextPage_TheButtonAsksForIt", async () => {
    const onMore = vi.fn();
    renderLoadMore({ onMore });

    await userEvent.click(screen.getByRole("button", { name: "Load more" }));

    expect(onMore).toHaveBeenCalledOnce();
  });

  it("LoadMore_Loading_ShowsTheLoadingRowInTheButtonsPlace", () => {
    renderLoadMore({ loading: true });

    expect(screen.getByText("Loading more members")).toBeVisible();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("LoadMore_LastPage_ShowsNoButton", () => {
    renderLoadMore({ hasMore: false });

    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("LoadMore_PageCame_TheLiveRegionSaysHowMany", () => {
    renderLoadMore({ announcement: "10 more members loaded." });

    expect(screen.getByText("10 more members loaded.")).toHaveAttribute(
      "aria-live",
      "polite",
    );
  });

  it("LoadMore_PageFailed_SaysSoWithTryAgainInTheButtonsPlace", async () => {
    const onMore = vi.fn();
    renderLoadMore({
      onMore,
      failed: { text: "Couldn’t load older activity", retryLabel: "Try again" },
    });

    expect(screen.getByText("Couldn’t load older activity")).toBeVisible();
    expect(
      screen.queryByRole("button", { name: "Load more" }),
    ).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(onMore).toHaveBeenCalledOnce();
  });
});
