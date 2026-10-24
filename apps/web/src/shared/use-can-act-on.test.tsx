import { BuiltInRole, Permission } from "@repo/api-client";
import { screen } from "@testing-library/react";
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
import {
  billingLead,
  customRole,
  mehmet,
  permissionsOf,
  renderInTenant,
  roleOf,
  supportLead,
  tenantAnswers,
  viewerAnswers,
} from "../../test/tenant";
import { holdsAll, useCanActOn } from "./use-can-act-on";

vi.mock("@clerk/nextjs", () => ({ useClerk: () => ({ signOut: vi.fn() }) }));

const server = setupServer();
beforeAll(() => server.listen({ onUnhandledFrame: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const role = (permissions: Permission[]) =>
  customRole("0198f0a2-0000-7000-8000-0000000000d1", "Probe", permissions);

describe("holdsAll", () => {
  it("HoldsAll_OwnerAndAnyBuiltInRole_True", () => {
    expect(
      [permissionsOf.owner, permissionsOf.admin, permissionsOf.member].map(
        (permissions) => holdsAll(permissionsOf.owner, role(permissions)),
      ),
    ).toEqual([true, true, true]);
  });

  it("HoldsAll_AdminOnTheOwner_False", () => {
    expect(holdsAll(permissionsOf.admin, role(permissionsOf.owner))).toBe(
      false,
    );
  });

  it("HoldsAll_CustomRoleWithAPermissionTheViewerLacks_False", () => {
    expect(holdsAll(supportLead.permissions, billingLead)).toBe(false);
  });

  it("HoldsAll_CustomRoleTheViewerFullyHolds_True", () => {
    expect(holdsAll(permissionsOf.admin, billingLead)).toBe(true);
  });

  it("HoldsAll_RoleWithOneExtraPermission_False", () => {
    const viewer = [Permission.TenantProfileRead, Permission.TenantMembersRead];
    expect(
      holdsAll(viewer, role([...viewer, Permission.TenantMembersDelete])),
    ).toBe(false);
  });

  it("HoldsAll_EmptySets_TrueForAnEmptyRoleAndFalseForAnEmptyViewer", () => {
    expect([
      holdsAll([], role([])),
      holdsAll([], role([Permission.TenantProfileRead])),
    ]).toEqual([true, false]);
  });
});

const gone = "0198f0a2-0000-7000-8000-0000000000ff";

function Probe() {
  const canActOn = useCanActOn();
  return (
    <p>
      {[
        roleOf.owner.id,
        roleOf.admin.id,
        roleOf.member.id,
        billingLead.id,
        gone,
      ]
        .map((id) => String(canActOn(id)))
        .join(" ")}
    </p>
  );
}

describe("useCanActOn", () => {
  it("CanActOn_Owner_ActsOnEveryRoleTheListKnows", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Owner, permissionsOf.owner, [billingLead]),
    );

    renderInTenant(<Probe />);

    expect(
      await screen.findByText("true true true true false"),
    ).toBeInTheDocument();
  });

  it("CanActOn_Admin_ActsOnEveryRoleButTheOwner", async () => {
    server.use(
      ...tenantAnswers(BuiltInRole.Admin, permissionsOf.admin, [billingLead]),
    );

    renderInTenant(<Probe />);

    expect(
      await screen.findByText("false true true true false"),
    ).toBeInTheDocument();
  });

  it("CanActOn_CustomRole_ActsOnlyOnRolesItFullyHolds", async () => {
    server.use(
      ...viewerAnswers(
        supportLead,
        supportLead.permissions,
        { ...mehmet, role: supportLead },
        [supportLead, billingLead],
      ),
    );

    renderInTenant(<Probe />);

    expect(
      await screen.findByText("false false true false false"),
    ).toBeInTheDocument();
  });
});
