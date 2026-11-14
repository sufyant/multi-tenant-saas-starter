import type { Page } from "@playwright/test";
import {
  ErrorCode,
  type FeatureFlagChangeResponse,
  FeatureFlagChangeResponseKind,
  type FeatureFlagDetailResponse,
  type FeatureFlagResponse,
  FeatureFlagResponseCleanup,
  FeatureFlagResponseType,
  type FeatureFlagsResponse,
} from "@repo/api-client";
import { organisations, problem } from "./api";

// The flags that the feature flags' frames draw (12.01 to 12.33, 4.09): six flags the code defines and one it removed.

const daysAgo = (days: number) =>
  new Date(Date.now() - days * 86_400_000).toISOString();
const person = (name: string) => ({ name, deleted: false });
const deniz = person("Deniz Kaya");
const selin = person("Selin Arslan");
const hannah = person("Hannah Weber");

const flag = (
  key: string,
  values: Partial<FeatureFlagResponse>,
): FeatureFlagResponse => ({
  key,
  type: FeatureFlagResponseType.Release,
  listedCount: 0,
  percentage: 0,
  everyoneElse: false,
  cleanup: FeatureFlagResponseCleanup.InUse,
  cleanupSince: null,
  removedAt: null,
  lastChange: { at: "2026-10-28T13:02:00Z", actor: deniz },
  ...values,
});

export const frameFlags = {
  exportV2: flag("activity.export_v2", {
    everyoneElse: true,
    cleanup: FeatureFlagResponseCleanup.ReadyToRemove,
    cleanupSince: daysAgo(32),
    lastChange: { at: "2026-10-01T10:00:00Z", actor: selin },
  }),
  searchV2: flag("users.search_v2", {
    everyoneElse: true,
    cleanup: FeatureFlagResponseCleanup.ReadyToRemove,
    cleanupSince: daysAgo(51),
    lastChange: { at: "2026-09-24T10:00:00Z", actor: deniz },
  }),
  digest: flag("notifications.digest", {
    cleanup: FeatureFlagResponseCleanup.Unused,
    cleanupSince: daysAgo(34),
    lastChange: { at: "2026-09-29T10:00:00Z", actor: hannah },
  }),
  bulkEdit: flag("roles.bulk_edit", { listedCount: 1, percentage: 10 }),
  newTable: flag("members.new_table", {
    listedCount: 3,
    lastChange: { at: "2026-10-30T10:00:00Z", actor: deniz },
  }),
  emailsSend: flag("emails.send", {
    type: FeatureFlagResponseType.KillSwitch,
    everyoneElse: true,
    lastChange: { at: "2026-01-12T10:00:00Z", actor: selin },
  }),
  csvImport: flag("invitations.csv_import", {
    everyoneElse: true,
    removedAt: "2026-10-20T15:05:00Z",
    lastChange: { at: "2026-10-20T15:05:00Z", actor: null },
  }),
};

const list: FeatureFlagsResponse = {
  flags: Object.values(frameFlags),
  counts: { all: 6, readyToRemove: 2, unused: 1, removedFromCode: 1 },
};

const change = (
  kind: FeatureFlagChangeResponseKind,
  at: string,
  values: Partial<FeatureFlagChangeResponse> = {},
): FeatureFlagChangeResponse => ({
  kind,
  from: null,
  to: null,
  tenant: null,
  revision: null,
  at,
  actor: deniz,
  ...values,
});

const added = (at: string, revision: string) =>
  change(FeatureFlagChangeResponseKind.AddedToCode, at, {
    revision,
    actor: null,
  });

const detail = (
  value: FeatureFlagResponse,
  fields: Partial<FeatureFlagDetailResponse>,
): FeatureFlagDetailResponse => ({
  flag: value,
  description: "",
  defaultValue: value.type === FeatureFlagResponseType.KillSwitch,
  addedAt: "2026-10-14T10:20:00Z",
  addedRevision: "3f9e21c",
  removedRevision: null,
  listed: [],
  history: [],
  ...fields,
});

const northwind = {
  tenantId: organisations.northwind.id,
  name: "Northwind Traders",
  listedAt: "2026-10-21T08:30:00Z",
};

/** Each flag's page as the frames draw it (12.05, 12.10 to 12.12, 12.18). */
export const frameDetails: Record<string, FeatureFlagDetailResponse> = {
  "roles.bulk_edit": detail(frameFlags.bulkEdit, {
    description: "Edit the permissions of several roles at once.",
    listed: [northwind],
    history: [
      change(
        FeatureFlagChangeResponseKind.PercentageChanged,
        "2026-10-28T13:02:00Z",
        { from: "0", to: "10" },
      ),
      change(FeatureFlagChangeResponseKind.Listed, "2026-10-21T08:30:00Z", {
        tenant: { id: northwind.tenantId, name: northwind.name },
      }),
      added("2026-10-14T10:20:00Z", "3f9e21c"),
    ],
  }),
  "activity.export_v2": detail(frameFlags.exportV2, {
    description: "Export the Activity log as CSV with the new columns.",
    addedAt: "2026-09-02T09:00:00Z",
    addedRevision: "8d2a6b4",
    history: [
      change(
        FeatureFlagChangeResponseKind.EveryoneElseChanged,
        "2026-10-01T10:00:00Z",
        { from: "off", to: "on", actor: selin },
      ),
      change(
        FeatureFlagChangeResponseKind.PercentageChanged,
        "2026-09-18T15:20:00Z",
        { from: "10", to: "50", actor: selin },
      ),
      added("2026-09-02T09:00:00Z", "8d2a6b4"),
    ],
  }),
  "notifications.digest": detail(frameFlags.digest, {
    description: "Send one email a day with the unread notifications.",
    addedAt: "2026-09-29T10:00:00Z",
    addedRevision: "5b1e8f2",
    history: [added("2026-09-29T10:00:00Z", "5b1e8f2")],
  }),
  "emails.send": detail(frameFlags.emailsSend, {
    description: "Sends every email. Turn it off at once if emails go wrong.",
    addedAt: "2026-01-12T10:00:00Z",
    addedRevision: "c5e07f1",
    history: [added("2026-01-12T10:00:00Z", "c5e07f1")],
  }),
  "invitations.csv_import": detail(frameFlags.csvImport, {
    description: "Invite members from a CSV file.",
    addedAt: "2026-08-03T09:00:00Z",
    addedRevision: "1b9d3e7",
    removedRevision: "7c41d09",
    history: [
      change(
        FeatureFlagChangeResponseKind.RemovedFromCode,
        "2026-10-20T15:05:00Z",
        { revision: "7c41d09", actor: null },
      ),
      change(
        FeatureFlagChangeResponseKind.EveryoneElseChanged,
        "2026-09-15T09:05:00Z",
        { from: "off", to: "on" },
      ),
      added("2026-08-03T09:00:00Z", "1b9d3e7"),
    ],
  }),
};

/** emails.send off for everyone since 10:12 (12.27). */
export const emailsOff = (() => {
  const at = new Date();
  at.setHours(10, 12, 0, 0);
  const base = frameDetails["emails.send"];
  if (!base) throw new Error("No emails.send.");
  return {
    ...base,
    flag: { ...base.flag, everyoneElse: false },
    history: [
      change(
        FeatureFlagChangeResponseKind.EveryoneElseChanged,
        at.toISOString(),
        { from: "on", to: "off" },
      ),
      ...base.history,
    ],
  };
})();

/**
 * GetFeatureFlags, GetFeatureFlag and the preview as the frames draw them. `list` replaces the list (12.13), `details` a page
 * (12.27), and `previewCount` the count every confirmation says.
 */
export const mockFrameFlags =
  ({
    flags = list,
    details = {},
    previewCount = 2,
  }: {
    flags?: FeatureFlagsResponse;
    details?: Record<string, FeatureFlagDetailResponse>;
    previewCount?: number;
  } = {}) =>
  async (page: Page) => {
    await page.route("**/api/v1/platform/feature-flags", (route) =>
      route.fulfill({ json: flags }),
    );
    await page.route("**/api/v1/platform/feature-flags/*", (route) => {
      const key = decodeURIComponent(
        new URL(route.request().url()).pathname.split("/").at(-1) ?? "",
      );
      const found = details[key] ?? frameDetails[key];
      return route.fulfill(
        found ? { json: found } : problem(404, ErrorCode.FeatureFlagNotFound),
      );
    });
    await page.route("**/api/v1/platform/feature-flags/*/preview", (route) =>
      route.fulfill({ json: { affectedCount: previewCount } }),
    );
  };

/** GetFeatureFlags or GetFeatureFlag never answers (12.14, 12.29). */
export const flagsWait = async (page: Page) => {
  await page.route(
    "**/api/v1/platform/feature-flags**",
    () => new Promise(() => {}),
  );
};

/** GetFeatureFlags or GetFeatureFlag fails (12.15, 12.30). */
export const flagsFail = async (page: Page) => {
  await page.route("**/api/v1/platform/feature-flags**", (route) =>
    route.fulfill(problem(500, ErrorCode.ServerError)),
  );
};
