import type { Permission } from "@repo/api-client";
import type { LucideIcon } from "@repo/ui/components/icon";

/**
 * A section of the product in the sidebar: its pages are under `/[tenantSlug]/<id>`, its name is the message `shell.<id>` and the
 * sidebar shows it only to a member whose role holds its permission (F5, F59).
 */
export type ProductSection = {
  id: string;
  icon: LucideIcon;
  permission: Permission;
};

/**
 * The product's own sections, after Home, in this order (the API's X1). The template ships this list empty and never changes it: a
 * product adds its sections here and their names in `messages/product`. The command menu lists them under Go to.
 */
export const productSections: readonly ProductSection[] = [];
