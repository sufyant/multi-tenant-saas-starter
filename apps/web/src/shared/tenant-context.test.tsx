import {
  ErrorCode,
  getListMembersQueryOptions,
  type MyTenantResponse,
  MyTenantResponseStatus,
  Permission,
  removeMember,
} from "@repo/api-client";
import {
  getGetMyTenantMockHandler,
  getGetMyTenantResponseMock,
} from "@repo/api-client/msw";
import { useMutation, useQuery } from "@tanstack/react-query";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse, http } from "msw";
import { setupServer } from "msw/node";
import {
  afterAll,
  afterEach,
  beforeAll,
  describe,
  expect,
  it,
  vi,
} from "vitest";
import { renderWithProviders } from "../../test/render";
import { problem, roleOf } from "../../test/tenant";
import { TenantProvider, useCan, useTenant } from "./tenant-context";

vi.mock("@clerk/nextjs", () => ({
  useClerk: () => ({ signOut: vi.fn() }),
  useUser: () => ({
    user: { primaryEmailAddress: { emailAddress: "ali.kaya@gmail.com" } },
  }),
}));
const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const northwind: MyTenantResponse = {
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f",
  name: "Northwind Traders",
  slug: "northwind",
  status: "active",
  role: roleOf.member,
};

function Probe() {
  const { tenant } = useTenant();
  const can = useCan();
  return (
    <p>
      {tenant.name} {String(can(Permission.TenantMembersRead))}{" "}
      {String(can(Permission.TenantMembersDelete))}
    </p>
  );
}

const tenants = (items: MyTenantResponse[]) =>
  http.get("*/v1/me/tenants", () =>
    HttpResponse.json({ items, page: 1, pageSize: 100, total: items.length }),
  );

const atlas: MyTenantResponse = {
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e30",
  name: "Atlas Logistics",
  slug: "atlas-logistics",
  status: MyTenantResponseStatus.Suspended,
  role: roleOf.admin,
};

const kestrel: MyTenantResponse = {
  id: "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e31",
  name: "Kestrel Labs",
  slug: "kestrel",
  status: MyTenantResponseStatus.Closed,
  role: roleOf.member,
};

/** ListMyTenants: Northwind is active at the first read and suspended at every later one. */
function suspendedAfterTheFirstRead() {
  let reads = 0;
  return http.get("*/v1/me/tenants", () => {
    reads += 1;
    const status =
      reads <= 2
        ? MyTenantResponseStatus.Active
        : MyTenantResponseStatus.Suspended;
    return HttpResponse.json({
      items: [{ ...northwind, status }],
      page: 1,
      pageSize: 100,
      total: 1,
    });
  });
}

/** A read of a page: the members, whose error the page leaves to its own list. */
function MembersReader({ tenantId }: { tenantId?: string }) {
  const { tenant } = useTenant();
  const members = useQuery(getListMembersQueryOptions(tenantId ?? tenant.id));
  return <p>Members {members.status}</p>;
}

/** ListMyTenants answers Northwind, active, and counts its reads. */
function countsTheReads(reads: { count: number }) {
  return http.get("*/v1/me/tenants", () => {
    reads.count += 1;
    return HttpResponse.json({
      items: [northwind],
      page: 1,
      pageSize: 100,
      total: 1,
    });
  });
}

/** A command of a page: remove a member, whose refusal the page leaves to its own dialog. */
function RemoveButton() {
  const { tenant } = useTenant();
  const remove = useMutation({
    mutationFn: ({ tenantId, userId }: { tenantId: string; userId: string }) =>
      removeMember(tenantId, userId),
  });
  return (
    <button
      type="button"
      onClick={() =>
        remove.mutate({
          tenantId: tenant.id,
          userId: "0198f0a2-0000-7000-8000-000000000004",
        })
      }
    >
      Remove
    </button>
  );
}

describe("TenantProvider", () => {
  it("TenantProvider_SlugInTheList_ProvidesTheTenantAndItsPermissions", async () => {
    server.use(
      tenants([northwind]),
      getGetMyTenantMockHandler(
        getGetMyTenantResponseMock({
          ...northwind,
          permissions: [Permission.TenantMembersRead],
        }),
      ),
    );

    renderWithProviders(
      <TenantProvider slug="northwind">
        <Probe />
      </TenantProvider>,
    );

    expect(
      await screen.findByText("Northwind Traders true false"),
    ).toBeInTheDocument();
  });

  it("TenantProvider_SlugNotInTheList_SaysNoAccessYet", async () => {
    server.use(tenants([]));

    renderWithProviders(
      <TenantProvider slug="atlas">
        <Probe />
      </TenantProvider>,
    );

    expect(
      await screen.findByRole("heading", { name: "No access yet" }),
    ).toBeInTheDocument();
  });

  it("TenantProvider_SuspendedInTheList_ShowsTheSuspendedPageWithNoReason", async () => {
    server.use(tenants([atlas]));

    renderWithProviders(
      <TenantProvider slug="atlas-logistics">
        <Probe />
      </TenantProvider>,
    );

    expect(
      await screen.findByRole("heading", {
        name: "Atlas Logistics is suspended",
      }),
    ).toBeInTheDocument();
    expect(screen.getByText("Suspended")).toBeInTheDocument();
    expect(
      screen.getByText(
        "You cannot open this organisation right now. Your data and your membership are kept. Contact your organisation’s Owner if you need to know more.",
      ),
    ).toBeInTheDocument();
    expect(screen.getByText("atlas-logistics")).toBeInTheDocument();
    expect(screen.getByText("Admin")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Sign out" }),
    ).toBeInTheDocument();
    expect(screen.queryByText(/Atlas Logistics true/)).not.toBeInTheDocument();
  });

  it("TenantProvider_OneOrganisationOnly_OffersNoOtherOrganisation", async () => {
    server.use(tenants([atlas]));

    renderWithProviders(
      <TenantProvider slug="atlas-logistics">
        <Probe />
      </TenantProvider>,
    );

    await screen.findByRole("heading", {
      name: "Atlas Logistics is suspended",
    });
    expect(
      screen.queryByRole("button", { name: "Choose another organisation" }),
    ).not.toBeInTheDocument();
  });

  // The API refuses a member of an organisation whose deletion has started as a closed one's, so its page is the Closed board.
  it("TenantProvider_Purging_ShowsTheClosedPage", async () => {
    server.use(
      tenants([{ ...kestrel, status: MyTenantResponseStatus.Purging }]),
    );

    renderWithProviders(
      <TenantProvider slug="kestrel">
        <Probe />
      </TenantProvider>,
    );

    expect(
      await screen.findByRole("heading", { name: "Kestrel Labs is closed" }),
    ).toBeInTheDocument();
  });

  it("TenantProvider_ClosedWithOtherOrganisations_OffersTheChooser", async () => {
    server.use(tenants([kestrel, northwind]));

    renderWithProviders(
      <TenantProvider slug="kestrel">
        <Probe />
      </TenantProvider>,
    );

    expect(
      await screen.findByRole("heading", { name: "Kestrel Labs is closed" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "This organisation was closed and cannot be opened. Your data is kept for now. Contact your organisation’s Owner if you need to know more.",
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Choose another organisation" }),
    ).toHaveAttribute("href", "/");
  });

  it("TenantProvider_GetMyTenantSaysClosed_ShowsTheClosedPage", async () => {
    server.use(
      tenants([northwind]),
      http.get(`*/v1/tenants/${northwind.id}`, () =>
        problem(403, ErrorCode.TenantClosed),
      ),
    );

    renderWithProviders(
      <TenantProvider slug="northwind">
        <Probe />
      </TenantProvider>,
    );

    expect(
      await screen.findByRole("heading", {
        name: "Northwind Traders is closed",
      }),
    ).toBeInTheDocument();
  });

  it("TenantProvider_CommandSaysSuspended_ShowsTheSuspendedPage", async () => {
    server.use(
      suspendedAfterTheFirstRead(),
      getGetMyTenantMockHandler(
        getGetMyTenantResponseMock({
          ...northwind,
          permissions: [Permission.TenantMembersRead],
        }),
      ),
      http.delete(`*/v1/tenants/${northwind.id}/members/:userId`, () =>
        problem(403, ErrorCode.TenantSuspended),
      ),
    );
    renderWithProviders(
      <TenantProvider slug="northwind">
        <RemoveButton />
      </TenantProvider>,
    );

    await userEvent.click(
      await screen.findByRole("button", { name: "Remove" }),
    );

    expect(
      await screen.findByRole("heading", {
        name: "Northwind Traders is suspended",
      }),
    ).toBeInTheDocument();
  });

  it("TenantProvider_ReadSaysSuspended_ShowsTheSuspendedPage", async () => {
    server.use(
      suspendedAfterTheFirstRead(),
      getGetMyTenantMockHandler(
        getGetMyTenantResponseMock({
          ...northwind,
          permissions: [Permission.TenantMembersRead],
        }),
      ),
      http.get(`*/v1/tenants/${northwind.id}/members`, () =>
        problem(403, ErrorCode.TenantSuspended),
      ),
    );

    renderWithProviders(
      <TenantProvider slug="northwind">
        <MembersReader />
      </TenantProvider>,
    );

    expect(
      await screen.findByRole("heading", {
        name: "Northwind Traders is suspended",
      }),
    ).toBeInTheDocument();
  });

  it("TenantProvider_AnotherTenantsReadSaysSuspended_ReadsNoListAgain", async () => {
    const reads = { count: 0 };
    server.use(
      countsTheReads(reads),
      getGetMyTenantMockHandler(
        getGetMyTenantResponseMock({
          ...northwind,
          permissions: [Permission.TenantMembersRead],
        }),
      ),
      http.get(`*/v1/tenants/${atlas.id}/members`, () =>
        problem(403, ErrorCode.TenantSuspended),
      ),
    );

    renderWithProviders(
      <TenantProvider slug="northwind">
        <MembersReader tenantId={atlas.id} />
      </TenantProvider>,
    );

    expect(await screen.findByText("Members error")).toBeInTheDocument();
    expect(reads.count).toBe(2);
    expect(screen.queryByRole("heading")).not.toBeInTheDocument();
  });

  // A suspended or deleted user gets 403 on every request (A9): the account's page, from the first read or a later one.
  const blocked = [
    [ErrorCode.UserSuspended, "Your account is suspended"],
    [ErrorCode.UserDeleted, "This account was deleted"],
  ];

  it.each(blocked)(
    "TenantProvider_ListMyTenantsSays_%s_ShowsTheAccountPage",
    async (code, title) => {
      server.use(http.get("*/v1/me/tenants", () => problem(403, code)));

      renderWithProviders(
        <TenantProvider slug="northwind">
          <Probe />
        </TenantProvider>,
      );

      expect(
        await screen.findByRole("heading", { name: title }),
      ).toBeInTheDocument();
      expect(screen.getByText("ali.kaya@gmail.com")).toBeInTheDocument();
    },
  );

  it.each(blocked)(
    "TenantProvider_LaterReadSays_%s_ShowsTheAccountPageInPlaceOfTheContent",
    async (code, title) => {
      server.use(
        countsTheReads({ count: 0 }),
        getGetMyTenantMockHandler(
          getGetMyTenantResponseMock({
            ...northwind,
            permissions: [Permission.TenantMembersRead],
          }),
        ),
        http.get(`*/v1/tenants/${northwind.id}/members`, () =>
          problem(403, code),
        ),
      );

      renderWithProviders(
        <TenantProvider slug="northwind">
          <MembersReader />
        </TenantProvider>,
      );

      expect(
        await screen.findByRole("heading", { name: title }),
      ).toBeInTheDocument();
      expect(screen.queryByText(/^Members/)).not.toBeInTheDocument();
    },
  );

  it.each(blocked)(
    "TenantProvider_CommandSays_%s_ReadsGetMeAgainAndShowsTheAccountPage",
    async (code, title) => {
      server.use(
        countsTheReads({ count: 0 }),
        getGetMyTenantMockHandler(
          getGetMyTenantResponseMock({
            ...northwind,
            permissions: [Permission.TenantMembersRead],
          }),
        ),
        // A9 refuses every request, so GetMe, read again after the command, answers the same.
        http.get("*/v1/me", () => problem(403, code)),
        http.delete(`*/v1/tenants/${northwind.id}/members/:userId`, () =>
          problem(403, code),
        ),
      );
      renderWithProviders(
        <TenantProvider slug="northwind">
          <RemoveButton />
        </TenantProvider>,
      );

      await userEvent.click(
        await screen.findByRole("button", { name: "Remove" }),
      );

      expect(
        await screen.findByRole("heading", { name: title }),
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: "Remove" }),
      ).not.toBeInTheDocument();
    },
  );
});
