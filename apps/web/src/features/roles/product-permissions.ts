import type { Permission } from "@repo/api-client";

/** A group of the roles screen: its name is the message `roles.groups.<id>`, and each permission's label is `permissions.<name>`. */
export type PermissionGroup = {
  id: string;
  permissions: readonly Permission[];
};

/**
 * The product's permissions in the groups of the roles screen, after the template's (the API's X1). The template ships this list
 * empty and never changes it: a product adds a group for each of its areas, and the words of each group and each permission in
 * `messages/product` (`roles.groups.<id>`, `permissions.<name>` and, for the line under it, `roles.hints.<name>`).
 */
export const productPermissionGroups: readonly PermissionGroup[] = [];
