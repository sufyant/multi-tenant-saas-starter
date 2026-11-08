import { describe, expect, it } from "vitest";
import { nextPage, rowsIn } from "./pages";

describe("pages", () => {
  it("NextPage_RowsLeft_IsThePageAfter", () => {
    expect(nextPage({ page: 1, pageSize: 50, total: 51 })).toBe(2);
  });

  it("NextPage_LastPage_IsNone", () => {
    expect(nextPage({ page: 2, pageSize: 50, total: 100 })).toBeUndefined();
  });

  it("RowsIn_Pages_CountsTheirRows", () => {
    expect(rowsIn({ pages: [{ items: [1, 2] }, { items: [3] }] })).toBe(3);
  });

  it("RowsIn_NoPageYet_IsNone", () => {
    expect(rowsIn(undefined)).toBeUndefined();
  });
});
