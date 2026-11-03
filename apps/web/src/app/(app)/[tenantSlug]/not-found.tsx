"use client";

import { NotFound } from "@repo/ui/components/not-found";
import Link from "next/link";
import { useParams } from "next/navigation";

// An old link or a page that moved, inside the organisation's shell: Home is the way on (the Page not found screens).
export default function TenantNotFound() {
  const { tenantSlug } = useParams<{ tenantSlug: string }>();
  return <NotFound kind="page" href={`/${tenantSlug}`} linkComponent={Link} />;
}
