import {
  ErrorCode,
  getExportMembersUrl,
  getGetMeUrl,
  getGetMyTenantUrl,
  getListMembersUrl,
  type ListPageOfMemberResponse,
  type MeResponse,
  type MyTenantDetailResponse,
  schemas,
} from "@repo/api-client";
import { calendarDay, isTimeZone } from "@repo/ui/lib/format-date-time";
import type { getTranslations } from "next-intl/server";
import {
  type ExportApi,
  exportFile,
  problem,
  readPage,
} from "@/shared/export-file";
import { roleNameOf } from "@/shared/roles";

type Texts = {
  members: Awaited<ReturnType<typeof getTranslations<"members">>>;
  roleNames: Awaited<ReturnType<typeof getTranslations<"shell.roleNames">>>;
};

/**
 * The members' CSV file (8.08 to 8.16): the list as the screen filters and sorts it, every page, in the reader's language. Its
 * name is `<address>-members-<day>.csv`, the day in the person's time zone, else the browser's, which the screen sends (F18).
 */
export async function membersFile(
  api: ExportApi,
  tenantId: string,
  search: URLSearchParams,
  { members: t, roleNames }: Texts,
): Promise<Response> {
  // A value the list does not take answers 400 before any call, so no audit entry says that a file left (F4).
  const filter = schemas.ListMembersQueryParams.pick({
    q: true,
    roleId: true,
    sort: true,
  }).safeParse({
    // The API trims the search before it counts its length (ListMembers' q), so the file does the same.
    q: search.get("q")?.trim() || undefined,
    roleId: search.get("roleId") || undefined,
    sort: search.get("sort") || undefined,
  });
  const browserZone = search.get("timeZone");
  if (!filter.success || (browserZone && !isTimeZone(browserZone)))
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
    const pageSize = schemas.listMembersQueryPageSizeMax;
    let written = 0;
    for (let page = 1; ; page++) {
      const list = await readPage<ListPageOfMemberResponse>(
        api,
        getListMembersUrl(tenantId, { ...filter.data, page, pageSize }),
      );
      for (const member of list.items)
        yield [
          member.deleted ? t("deletedUser") : (member.name ?? ""),
          member.email ?? "",
          roleNameOf(member.role, (builtIn) => roleNames(builtIn)),
          calendarDay(member.joinedAt, { timeZone: zone }),
        ];
      written += list.items.length;
      if (list.items.length === 0 || written >= list.total) return;
    }
  }

  return exportFile(api, {
    audit: getExportMembersUrl(tenantId),
    name: `${slug}-members-${calendarDay(new Date(), { timeZone: zone })}.csv`,
    header: [t("name"), t("email"), t("role"), t("joined")],
    rows: rows(),
  });
}
