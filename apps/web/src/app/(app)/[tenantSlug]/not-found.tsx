"use client";

import { NotFound } from "@repo/ui/components/not-found";
import Link from "next/link";
import { useParams } from "next/navigation";
import { OrganisationShell } from "@/features/shell/tenant-shell";

// An old link or a page that moved, inside the organisation's shell: Home is the way on (the Page not found screens). The
// [...missing] page that ends here is outside the (shell) group, so this page draws the shell itself.
export default function TenantNotFound() {
  const { tenantSlug } = useParams<{ tenantSlug: string }>();
  return (
    <OrganisationShell slug={tenantSlug} notFound>
      <NotFound
        kind="page"
        size="page"
        href={`/${tenantSlug}`}
        linkComponent={Link}
      />
    </OrganisationShell>
  );
}
