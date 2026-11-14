import {
  type FeatureFlagChangeResponse,
  FeatureFlagChangeResponseKind,
  type FeatureFlagDetailResponse,
  type FeatureFlagResponse,
  FeatureFlagResponseCleanup,
  FeatureFlagResponseType,
  type FeatureFlagsResponse,
} from "@repo/api-client";
import { HttpResponse, http } from "msw";

// The flags of the feature flags' frames (12.01 to 12.12): the sample data the tests give the screens.

const deniz = { name: "Deniz Kaya", deleted: false };

/** One flag of the list, in use and off, with the fields a test changes. */
export const flagOf = (
  key: string,
  values: Partial<FeatureFlagResponse> = {},
): FeatureFlagResponse => ({
  key,
  type: FeatureFlagResponseType.Release,
  listedCount: 0,
  percentage: 0,
  everyoneElse: false,
  cleanup: FeatureFlagResponseCleanup.InUse,
  cleanupSince: null,
  removedAt: null,
  lastChange: { at: "2026-10-28T14:02:00Z", actor: deniz },
  ...values,
});

/** roles.bulk_edit (12.05): Northwind listed, 10% and everyone else off. */
export const bulkEdit = flagOf("roles.bulk_edit", {
  listedCount: 1,
  percentage: 10,
});

/** emails.send (12.11): the kill switch, on for everyone. */
export const emailsSend = flagOf("emails.send", {
  type: FeatureFlagResponseType.KillSwitch,
  everyoneElse: true,
  lastChange: { at: "2026-01-12T10:00:00Z", actor: null },
});

/** activity.export_v2 (12.10): on for everyone for 32 days. */
export const exportV2 = flagOf("activity.export_v2", {
  everyoneElse: true,
  cleanup: FeatureFlagResponseCleanup.ReadyToRemove,
  cleanupSince: "2026-10-01T11:00:00Z",
});

/** notifications.digest (12.18): off everywhere for 34 days. */
export const digest = flagOf("notifications.digest", {
  cleanup: FeatureFlagResponseCleanup.Unused,
  cleanupSince: "2026-09-29T10:00:00Z",
});

/** invitations.csv_import (12.12): removed from code. */
export const csvImport = flagOf("invitations.csv_import", {
  everyoneElse: true,
  removedAt: "2026-10-20T16:05:00Z",
});

/** The list of 12.01: All counts the flags the code defines; the removed one counts only in its tab. */
export const flagsList: FeatureFlagsResponse = {
  flags: [bulkEdit, emailsSend, exportV2, digest, csvImport],
  counts: { all: 4, readyToRemove: 1, unused: 1, removedFromCode: 1 },
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

export const northwindId = "0198f0a2-7b3c-7c1d-9a4e-2f6b8c0d1e2f";

/** roles.bulk_edit's page (12.05), with its three lines of history. */
export const bulkEditDetail: FeatureFlagDetailResponse = {
  flag: bulkEdit,
  description: "Edit the permissions of several roles at once.",
  defaultValue: false,
  addedAt: "2026-10-14T11:20:00Z",
  addedRevision: "3f9e21c",
  removedRevision: null,
  listed: [
    {
      tenantId: northwindId,
      name: "Northwind Traders",
      listedAt: "2026-10-21T09:30:00Z",
    },
  ],
  history: [
    change(
      FeatureFlagChangeResponseKind.PercentageChanged,
      "2026-10-28T14:02:00Z",
      {
        from: "0",
        to: "10",
      },
    ),
    change(FeatureFlagChangeResponseKind.Listed, "2026-10-21T09:30:00Z", {
      tenant: { id: northwindId, name: "Northwind Traders" },
    }),
    change(FeatureFlagChangeResponseKind.AddedToCode, "2026-10-14T11:20:00Z", {
      revision: "3f9e21c",
      actor: null,
    }),
  ],
};

/** emails.send's page, off for everyone since 10:12 (12.27). */
export const emailsOffDetail: FeatureFlagDetailResponse = {
  flag: { ...emailsSend, everyoneElse: false },
  description: "Sends every email. Turn it off at once if emails go wrong.",
  defaultValue: true,
  addedAt: "2026-01-12T10:00:00Z",
  addedRevision: "c5e07f1",
  removedRevision: null,
  listed: [],
  history: [
    change(
      FeatureFlagChangeResponseKind.EveryoneElseChanged,
      "2026-11-02T10:12:00Z",
      {
        from: "on",
        to: "off",
      },
    ),
    change(FeatureFlagChangeResponseKind.AddedToCode, "2026-01-12T10:00:00Z", {
      revision: "c5e07f1",
      actor: null,
    }),
  ],
};

/** invitations.csv_import's page (12.12): read-only. */
export const csvImportDetail: FeatureFlagDetailResponse = {
  flag: csvImport,
  description: "Invite members from a CSV file.",
  defaultValue: false,
  addedAt: "2026-08-03T09:00:00Z",
  addedRevision: "1b9d3e7",
  removedRevision: "7c41d09",
  listed: [],
  history: [
    change(
      FeatureFlagChangeResponseKind.RemovedFromCode,
      "2026-10-20T16:05:00Z",
      {
        revision: "7c41d09",
        actor: null,
      },
    ),
  ],
};

/** GetFeatureFlags. */
export const flagsAnswer = (list: FeatureFlagsResponse = flagsList) =>
  http.get("*/v1/platform/feature-flags", () => HttpResponse.json(list));

/** GetFeatureFlag of one detail. */
export const flagAnswer = (detail: FeatureFlagDetailResponse) =>
  http.get(`*/v1/platform/feature-flags/${detail.flag.key}`, () =>
    HttpResponse.json(detail),
  );

/** GetFeatureFlagChangePreview: it records each body and answers the count. */
export const previewAnswer = (affectedCount: number, bodies: unknown[] = []) =>
  http.post("*/v1/platform/feature-flags/:key/preview", async ({ request }) => {
    bodies.push(await request.json());
    return HttpResponse.json({ affectedCount });
  });
