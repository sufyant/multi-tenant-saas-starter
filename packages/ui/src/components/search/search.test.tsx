import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { Search } from "./search";

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

const type = (text: string) =>
  fireEvent.change(screen.getByRole("searchbox", { name: "Search members" }), {
    target: { value: text },
  });

function renderSearch(value = "", onSearch = vi.fn()) {
  const view = render(
    <Search
      value={value}
      label="Search members"
      maxLength={200}
      onSearch={onSearch}
    />,
  );
  return { ...view, onSearch };
}

describe("Search", () => {
  it("Search_Typing_SendsTheText300msAfterTheLastKey", () => {
    const { onSearch } = renderSearch();

    type("a");
    act(() => vi.advanceTimersByTime(200));
    type("ay");
    act(() => vi.advanceTimersByTime(299));
    expect(onSearch).not.toHaveBeenCalled();
    act(() => vi.advanceTimersByTime(1));

    expect(onSearch).toHaveBeenCalledTimes(1);
    expect(onSearch).toHaveBeenCalledWith("ay");
  });

  it("Search_ValueChanges_FollowsIt", () => {
    const { rerender, onSearch } = renderSearch("ayşe");

    rerender(
      <Search
        value="kemal"
        label="Search members"
        maxLength={200}
        onSearch={onSearch}
      />,
    );
    act(() => vi.advanceTimersByTime(300));

    expect(screen.getByRole("searchbox")).toHaveValue("kemal");
    expect(onSearch).not.toHaveBeenCalled();
  });

  it("Search_Field_HasTheListsLimitAndItsLabelAsPlaceholder", () => {
    renderSearch();

    const field = screen.getByRole("searchbox", { name: "Search members" });
    expect(field).toHaveAttribute("maxLength", "200");
    expect(field).toHaveAttribute("placeholder", "Search members");
  });
});
