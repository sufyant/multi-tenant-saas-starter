import { Permission } from "@repo/api-client";
import {
  ActivityIcon,
  type LucideIcon,
  SettingsIcon,
  ShieldIcon,
  UsersIcon,
} from "@repo/ui/components/icon";

/**
 * The organisation's pages of Settings, in the order of the settings menu, each with its read permission (F5, F59): the settings
 * menu and the command menu list them, and Home offers them as cards. `label` is the key under `shell`.
 */
export const organisationPages: readonly {
  id: "general" | "members" | "roles" | "activity";
  label: "general" | "members" | "roles" | "activityLog";
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
    id: "activity",
    label: "activityLog",
    icon: ActivityIcon,
    permission: Permission.TenantAuditRead,
  },
];
