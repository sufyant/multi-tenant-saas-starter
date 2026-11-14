import type { Page } from "@playwright/test";
import {
  type CursorPageOfTermsPersonResponse,
  ErrorCode,
  Language,
  type TermsPersonResponse,
  TermsPersonResponseStatus,
  type TermsVersionResponse,
  TermsVersionResponseState,
} from "@repo/api-client";
import { me, problem } from "./api";
import { frameUsers } from "./frame-users";

// The Terms of use frames (11.01 to 11.17): the three versions of 11.01, the 67 users who may accept, and the people of 11.05 and
// 11.06, with Deniz Kaya, the signed-in system admin of every frame, as the viewer.

const activeUserCount = 67;

const version = (
  n: number,
  fields: Partial<TermsVersionResponse>,
): TermsVersionResponse => ({
  id: `0198f0a2-0000-7000-8000-0000000f${String(n).padStart(4, "0")}`,
  effectiveDate: "2026-12-01",
  url: "https://starter.example/terms/2026-12",
  summary:
    "Clearer rules for data export, and 30 days’ notice before an organisation is deleted.",
  requiresAcceptance: true,
  state: TermsVersionResponseState.Scheduled,
  acceptedCount: 18,
  locked: true,
  sendsEmail: true,
  emailSentAt: "2026-11-02T09:00:00Z",
  createdAt: "2026-11-02T09:00:00Z",
  cancelledAt: null,
  ...fields,
});

export const frameTerms = {
  scheduled: version(1, {}),
  inForce: version(2, {
    effectiveDate: "2026-03-01",
    url: "https://starter.example/terms/2026-03",
    summary: "Plainer wording on data processing and support hours.",
    state: TermsVersionResponseState.InForce,
    acceptedCount: activeUserCount,
    emailSentAt: "2026-02-01T09:00:00Z",
  }),
  replaced: version(3, {
    effectiveDate: "2026-01-12",
    url: "https://starter.example/terms/2026-01",
    summary: null,
    requiresAcceptance: false,
    sendsEmail: false,
    emailSentAt: null,
    state: TermsVersionResponseState.Replaced,
    acceptedCount: 0,
    locked: false,
  }),
};

/** The version 11.04 adds: no one accepted it yet. */
const added = { ...frameTerms.scheduled, acceptedCount: 0, locked: false };

const personOf = (
  user: { id: string; name: string | null; email: string | null },
  acceptedAt: string | null,
): TermsPersonResponse => ({
  userId: user.id,
  name: user.name,
  email: user.email,
  status: acceptedAt
    ? TermsPersonResponseStatus.Accepted
    : TermsPersonResponseStatus.NotYet,
  acceptedAt,
});

const people = {
  accepted: [
    personOf(frameUsers.ayse, "2026-11-02T09:14:00Z"),
    personOf(frameUsers.omar, "2026-11-01T17:40:00Z"),
    personOf(frameUsers.rosa, "2026-11-01T08:02:00Z"),
  ],
  notYet: [
    personOf(frameUsers.aiko, null),
    personOf(frameUsers.mark, null),
    personOf(frameUsers.nadia, null),
  ],
};

type Versions = () => TermsVersionResponse[];

/**
 * The terms of the frames: GetTermsOfUse with the versions, each version's page and its people. `versions` answers each read, so
 * a command can change what the next read shows.
 */
export const mockFrameTerms =
  (versions: Versions = () => Object.values(frameTerms)) =>
  async (page: Page) => {
    await page.route("**/api/v1/me", (route) =>
      route.fulfill({
        json: {
          ...me(Language.En),
          id: frameUsers.deniz.id,
          name: "Deniz Kaya",
          email: "deniz@platform.example",
        },
      }),
    );
    await page.route("**/api/v1/platform/terms", (route) =>
      route.fulfill({ json: { versions: versions(), activeUserCount } }),
    );
    await page.route("**/api/v1/platform/terms/versions/*", (route) => {
      if (route.request().method() !== "GET") return route.fallback();
      const id = new URL(route.request().url()).pathname.split("/").at(-1);
      const found = [...versions(), ...Object.values(frameTerms)].find(
        (each) => each.id === id,
      );
      return route.fulfill(
        found
          ? { json: { version: found, activeUserCount } }
          : problem(404, ErrorCode.TermsVersionNotFound),
      );
    });
    await page.route("**/api/v1/platform/terms/versions/*/people*", (route) => {
      const status = new URL(route.request().url()).searchParams.get("status");
      const answer: CursorPageOfTermsPersonResponse = {
        items:
          status === TermsPersonResponseStatus.NotYet
            ? people.notYet
            : people.accepted,
        nextCursor: null,
      };
      return route.fulfill({ json: answer });
    });
  };

/** GetTermsOfUse never answers (11.07). */
export const termsWait = async (page: Page) => {
  await mockFrameTerms()(page);
  await page.route("**/api/v1/platform/terms", () => new Promise(() => {}));
};

/** GetTermsOfUse fails (11.08). */
export const termsFail = async (page: Page) => {
  await mockFrameTerms()(page);
  await page.route("**/api/v1/platform/terms", (route) =>
    route.fulfill(problem(500, ErrorCode.ServerError)),
  );
};

/** Nothing scheduled (11.09); Add version adds 11.04's version, and the list then shows it. */
export const mockFrameAdd = async (page: Page) => {
  let created = false;
  await mockFrameTerms(() => [
    ...(created ? [added] : []),
    frameTerms.inForce,
    frameTerms.replaced,
  ])(page);
  await page.route("**/api/v1/platform/terms/versions", (route) => {
    created = true;
    return route.fulfill({ status: 201, json: added });
  });
};

/** Cancel version of the scheduled version (11.11); the list then shows the other two (11.13). */
export const mockFrameCancel = async (page: Page) => {
  let cancelled = false;
  await mockFrameTerms(() =>
    cancelled
      ? [frameTerms.inForce, frameTerms.replaced]
      : Object.values(frameTerms),
  )(page);
  await page.route(
    `**/api/v1/platform/terms/versions/${frameTerms.scheduled.id}/cancel`,
    (route) => {
      cancelled = true;
      return route.fulfill({
        json: {
          ...frameTerms.scheduled,
          state: TermsVersionResponseState.Cancelled,
          cancelledAt: "2026-11-14T09:00:00Z",
        },
      });
    },
  );
};
