import { getTranslations } from "next-intl/server";
import { activityFile } from "@/features/activity/activity-file";
import { bffWith } from "@/shared/bff";
import { exportApi, routeNotFoundUnlessUuid } from "@/shared/export-file";

// The Activity log's CSV file (F6): the audit command, then the file from the API's pages, as a stream.
export async function POST(
  request: Request,
  { params }: RouteContext<"/api/exports/tenants/[tenantId]/audit-log">,
): Promise<Response> {
  const { tenantId } = await params;
  const notFound = routeNotFoundUnlessUuid(tenantId);
  if (notFound) return notFound;
  return activityFile(
    exportApi(request, bffWith(fetch)),
    tenantId,
    new URL(request.url).searchParams,
    await getTranslations(),
  );
}
