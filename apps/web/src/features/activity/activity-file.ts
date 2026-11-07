import {
  type CursorPageOfAuditEntryResponse,
  ErrorCode,
  getExportAuditLogUrl,
  getGetMeUrl,
  getGetMyTenantUrl,
  getListAuditEntriesUrl,
  type MeResponse,
  type MyTenantDetailResponse,
  schemas,
} from "@repo/api-client";
import { calendarDay, isTimeZone } from "@repo/ui/lib/format-date-time";
import {
  type ExportApi,
  exportFile,
  problem,
  readPage,
} from "@/shared/export-file";
import { actorText, sentenceText, type Translator } from "./activity-text";
import { activityFileCap } from "./file-cap";

/** A moment as ISO 8601 in UTC to the second, as the dialog's example writes it: 2026-11-01T14:32:00Z (12.08). */
const utcSecond = (at: string) =>
  new Date(at).toISOString().replace(/\.\d{3}Z$/, "Z");

/**
 * The Activity log's CSV file (12.08): the newest entries first, up to `activityFileCap`, as Time (UTC), Who and What, in the
 * reader's language. Its name is `<address>-activity-log-<day>.csv`, the day in the person's time zone, else the browser's, which
 * the screen sends (F18); a date inside a sentence follows the same zone.
 */
export async function activityFile(
  api: ExportApi,
  tenantId: string,
  search: URLSearchParams,
  t: Translator,
): Promise<Response> {
  // A zone the file does not take answers 400 before any call, so no audit entry says that a file left (F4).
  const browserZone = search.get("timeZone");
  if (browserZone && !isTimeZone(browserZone))
    return problem(400, ErrorCode.RequestInvalid);

  const [me, tenant] = await Promise.all([
    api(getGetMeUrl()),
    api(getGetMyTenantUrl(tenantId)),
  ]);
  if (!me.ok) return me;
  if (!tenant.ok) return tenant;
  const { timeZone } = (await me.json()) as MeResponse;
  const { slug } = (await tenant.json()) as MyTenantDetailResponse;
  const zone = timeZone ?? browserZone ?? "UTC";

  async function* rows() {
    const limit = schemas.listAuditEntriesQueryLimitMax;
    let cursor: string | undefined;
    let written = 0;
    do {
      const page = await readPage<CursorPageOfAuditEntryResponse>(
        api,
        getListAuditEntriesUrl(tenantId, { cursor, limit }),
      );
      for (const entry of page.items) {
        if (written === activityFileCap) return;
        written++;
        yield [
          utcSecond(entry.occurredAt),
          actorText(entry, t),
          sentenceText(entry, t, zone),
        ];
      }
      cursor = page.nextCursor ?? undefined;
    } while (cursor && written < activityFileCap);
  }

  return exportFile(api, {
    audit: getExportAuditLogUrl(tenantId),
    name: `${slug}-activity-log-${calendarDay(new Date(), { timeZone: zone })}.csv`,
    header: [
      t("activity.fileTime"),
      t("activity.fileWho"),
      t("activity.fileWhat"),
    ],
    rows: rows(),
  });
}
