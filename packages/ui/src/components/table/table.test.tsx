import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { Status } from "../status";
import { Table, type TableColumn } from "./table";

type Member = {
  id: number;
  name: string;
  email: string;
  role: string;
  active: boolean;
};

const rows: Member[] = [
  {
    id: 1,
    name: "Ayşe Yılmaz",
    email: "ayse@northwind.io",
    role: "Owner",
    active: true,
  },
  {
    id: 2,
    name: "John Byrne",
    email: "john@northwind.io",
    role: "Member",
    active: false,
  },
];

const columns: TableColumn<Member>[] = [
  {
    key: "name",
    header: "Name",
    sortable: true,
    render: (row) => `${row.name} ${row.email}`,
    cardRender: (row) => row.name,
  },
  { key: "role", header: "Role" },
  {
    key: "status",
    header: "Status",
    card: "end",
    render: (row) => (
      <Status tone={row.active ? "success" : "warning"}>
        {row.active ? "Active" : "Suspended"}
      </Status>
    ),
  },
];

const pager = {
  previous: "Previous",
  next: "Next",
  page: (page: number, count: number) => `Page ${page} of ${count}`,
};
const props = {
  columns,
  rows,
  rowKey: "id" as const,
  label: "Members",
  rowCount: 2,
  page: 1,
  pageSize: 20,
  pager,
};

describe("Table", () => {
  it("Table_Wide_ShowsARowForEachRecord", () => {
    render(<Table {...props} />);

    expect(
      within(screen.getByRole("table", { name: "Members" })).getAllByRole(
        "row",
      ),
    ).toHaveLength(3);
  });

  it("Table_UnderSixHundredFortyPixels_TurnsIntoCards", () => {
    render(<Table {...props} />);

    const cards = screen.getByRole("list", { name: "Members" });
    expect(cards).toHaveClass("@min-[640px]:hidden");
    expect(screen.getByRole("table", { name: "Members" })).toHaveClass(
      "hidden",
      "@min-[640px]:table",
    );
    expect(within(cards).getAllByRole("listitem")[0]).toHaveTextContent(
      "Ayşe YılmazOwnerActive",
    );
  });

  it("Table_ClickASortableHeader_AsksTheApiForThatSort", async () => {
    const onSortChange = vi.fn();
    render(<Table {...props} onSortChange={onSortChange} />);

    await userEvent.click(screen.getByRole("button", { name: "Name" }));

    expect(onSortChange).toHaveBeenCalledWith({ key: "name", desc: false });
  });

  it("Table_MorePagesThanOne_PagesOnTheServer", async () => {
    const onPageChange = vi.fn();
    render(<Table {...props} rowCount={45} onPageChange={onPageChange} />);

    await userEvent.click(screen.getByRole("button", { name: "Next" }));

    expect(screen.getByText("Page 1 of 3")).toBeInTheDocument();
    expect(onPageChange).toHaveBeenCalledWith(2);
  });

  it("Table_InRtl_ShowsTheRows", () => {
    render(
      <div dir="rtl">
        <Table {...props} label="الأعضاء" />
      </div>,
    );

    expect(screen.getByRole("table", { name: "الأعضاء" })).toBeInTheDocument();
  });
});
