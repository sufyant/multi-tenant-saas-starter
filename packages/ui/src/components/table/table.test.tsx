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

  it("Table_WideColumn_ShowsOnlyInAContainerFromTheTableWideToken", () => {
    render(
      <Table
        {...props}
        columns={[...columns, { key: "email", header: "Email", show: "wide" }]}
      />,
    );

    const table = screen.getByRole("table", { name: "Members" });
    expect(
      within(table).getByRole("columnheader", { name: "Email" }),
    ).toHaveClass("hidden", "@table-wide:table-cell");
    expect(
      within(table).getByRole("cell", { name: "ayse@northwind.io" }),
    ).toHaveClass("hidden", "@table-wide:table-cell");
    expect(
      within(table).getByRole("columnheader", { name: "Role" }),
    ).not.toHaveClass("hidden");
  });

  it("Table_ClickASortableHeader_AsksTheApiForThatSort", async () => {
    const onSortChange = vi.fn();
    render(<Table {...props} onSortChange={onSortChange} />);

    await userEvent.click(screen.getByRole("button", { name: "Name" }));

    expect(onSortChange).toHaveBeenCalledWith({ key: "name", desc: false });
  });

  it("Table_ClickAColumnWithoutAStringValue_SortsAscendingFirst", async () => {
    const onSortChange = vi.fn();
    render(
      <Table
        {...props}
        columns={[{ ...columns[0], key: "person" } as TableColumn<Member>]}
        onSortChange={onSortChange}
      />,
    );

    await userEvent.click(screen.getByRole("button", { name: "Name" }));

    expect(onSortChange).toHaveBeenCalledWith({ key: "person", desc: false });
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

  // Table · Actions (424:2404): the ⋯ keeps its cell at rest and shows on the row's hover, on focus and while its menu is open.
  // jsdom draws no CSS, so the rule is the classes; the shots of 8.15 and 8.17 show the drawing.
  it("Table_RowActions_ShowOnHoverFocusOrOpenMenu", () => {
    render(
      <Table
        {...props}
        actions={(row) => <button type="button">Actions for {row.name}</button>}
        actionsLabel="Actions"
      />,
    );

    const table = screen.getByRole("table", { name: "Members" });
    const actions = within(table).getByRole("button", {
      name: "Actions for Ayşe Yılmaz",
    }).parentElement;
    expect(actions).toHaveClass(
      "opacity-0",
      "group-hover/row:opacity-100",
      "focus-within:opacity-100",
      "has-data-popup-open:opacity-100",
    );
  });

  // A touch screen has no hover, so the ⋯ always shows there (the task of 37a-shared-components).
  it("Table_RowActionsOnATouchScreen_AlwaysShow", () => {
    render(
      <Table
        {...props}
        actions={(row) => <button type="button">Actions for {row.name}</button>}
      />,
    );

    const table = screen.getByRole("table", { name: "Members" });
    expect(
      within(table).getByRole("button", { name: "Actions for Ayşe Yılmaz" })
        .parentElement,
    ).toHaveClass("pointer-coarse:opacity-100");
  });

  // Table · Card (258:1389): the phone card's ⋯ always shows.
  it("Table_CardActions_AlwaysShow", () => {
    render(
      <Table
        {...props}
        actions={(row) => <button type="button">Actions for {row.name}</button>}
      />,
    );

    const cards = screen.getByRole("list", { name: "Members" });
    expect(
      within(cards).getByRole("button", { name: "Actions for Ayşe Yılmaz" })
        .parentElement,
    ).not.toHaveClass("opacity-0");
  });

  // Table row (424:2472): the first column fills, and each other column keeps the width its frame gives it.
  it("Table_ColumnWithAWidth_KeepsIt", () => {
    render(
      <Table
        {...props}
        columns={[columns[0], { key: "role", header: "Role", width: "w-37.5" }]}
      />,
    );

    expect(
      within(screen.getByRole("table", { name: "Members" })).getByRole(
        "columnheader",
        { name: "Role" },
      ),
    ).toHaveClass("w-37.5");
  });
});
