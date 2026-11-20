import { Permission } from "@repo/api-client";
import {
  ActivityIcon,
  CreditCardIcon,
  GaugeIcon,
  type LucideIcon,
  SettingsIcon,
  ShieldIcon,
  UsersIcon,
} from "@repo/ui/components/icon";

/**
 * The organisation's pages of Settings, in the order of the settings menu, each with its read permission (F5, F59): the settings
 * menu and the command menu list them, and Home offers them as cards. `label` is the key under `shell`. Billing is shown only while
 * the deployment's billing is on (`useBillingShown`), and Usage only while the plan has a meter too (`useUsageShown`).
 */
export const organisationPages: readonly {
  id: "general" | "members" | "roles" | "billing" | "usage" | "activity";
  label: "general" | "members" | "roles" | "billing" | "usage" | "activityLog";
  icon: LucideIcon;
  permission: Permission;
}[] = [
  {
    id: "general",
    label: "general",
    icon: SettingsIcon,
    permission: Permission.TenantProfileRead,
  },
  {
    id: "members",
    label: "members",
    icon: UsersIcon,
    permission: Permission.TenantMembersRead,
  },
  {
    id: "roles",
    label: "roles",
    icon: ShieldIcon,
    permission: Permission.TenantRolesRead,
  },
  {
    id: "billing",
    label: "billing",
    icon: CreditCardIcon,
    permission: Permission.TenantBillingManage,
  },
  {
    id: "usage",
    label: "usage",
    icon: GaugeIcon,
    permission: Permission.TenantBillingManage,
  },
  {
    id: "activity",
    label: "activityLog",
    icon: ActivityIcon,
    permission: Permission.TenantAuditRead,
  },
];
