import { ReservedSlug } from "@repo/api-client";
import { notFound } from "next/navigation";

export default async function TenantLayout({
  children,
  params,
}: LayoutProps<"/[tenantSlug]">) {
  const { tenantSlug } = await params;
  // No organisation has a reserved slug (F58). Such an address is a top-level route's path that no page has, such as
  // /sign-in/missing, which Next.js gives to this segment: it is "Page not found" outside the shell.
  if (Object.values<string>(ReservedSlug).includes(tenantSlug)) notFound();
  // The shell is in the (shell) group. The shell draws its page only once it knows the organisation, which the server does not, so an
  // address that no page has stays outside it: its notFound() comes before the response starts, and the answer is a 404 (F59).
  return children;
}
