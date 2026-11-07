import { getTranslations } from "next-intl/server";
import { membersFile } from "@/features/members/members-file";
import { bffWith } from "@/shared/bff";
import { exportApi, routeNotFoundUnlessUuid } from "@/shared/export-file";

// The members' CSV file (F6): the audit command, then the file from the API's pages, as a stream.
export async function POST(
  request: Request,
  { params }: RouteContext<"/api/exports/tenants/[tenantId]/members">,
): Promise<Response> {
  const { tenantId } = await params;
  const notFound = routeNotFoundUnlessUuid(tenantId);
  if (notFound) return notFound;
  return membersFile(
    exportApi(request, bffWith(fetch)),
    tenantId,
    new URL(request.url).searchParams,
    {
      members: await getTranslations("members"),
      roleNames: await getTranslations("shell.roleNames"),
    },
  );
}
