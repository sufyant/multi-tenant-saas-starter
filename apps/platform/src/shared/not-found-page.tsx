"use client";

import { NotFound } from "@repo/ui/components/not-found";
import Link from "next/link";

/**
 * "Page not found" inside the shell for a record the API does not know (404): a plan or a user; "Organisation not found" for an
 * organisation (`tenant.not_found`). Organisations is the way on.
 */
export function NotFoundPage({
  of = "page",
}: {
  of?: "page" | "organisation";
}) {
  return <NotFound kind={of} href="/" linkComponent={Link} />;
}
