import {
  type CursorPageOfTermsPersonResponse,
  type TermsPersonResponse,
  TermsPersonResponseStatus,
  type TermsVersionResponse,
  TermsVersionResponseState,
} from "@repo/api-client";
import { HttpResponse, http } from "msw";

// The terms versions of the Terms of use frames (11.01): one scheduled from 1 Dec 2026, one in force from 1 Mar 2026 and one replaced
// from 12 Jan 2026, for the platform's tests. 67 users may accept.

export const activeUserCount = 67;

const version = (
  id: string,
  values: Partial<TermsVersionResponse>,
): TermsVersionResponse => ({
  id,
  effectiveDate: "2026-12-01",
  url: "https://starter.example/terms/2026-12",
  summary: null,
  requiresAcceptance: true,
  state: TermsVersionResponseState.Scheduled,
  acceptedCount: 0,
  locked: false,
  sendsEmail: true,
  emailSentAt: "2026-11-02T09:00:00Z",
  createdAt: "2026-11-02T09:00:00Z",
  cancelledAt: null,
  ...values,
});

export const scheduled = version("0198f0a2-0000-7000-8000-0000000000a1", {
  summary:
    "Clearer rules for data export, and 30 days’ notice before an organisation is deleted.",
  acceptedCount: 18,
  locked: true,
});

export const inForce = version("0198f0a2-0000-7000-8000-0000000000a2", {
  effectiveDate: "2026-03-01",
  url: "https://starter.example/terms/2026-03",
  summary: "Plainer wording on data processing and support hours.",
  state: TermsVersionResponseState.InForce,
  acceptedCount: activeUserCount,
  locked: true,
  emailSentAt: "2026-02-01T09:00:00Z",
});

export const replaced = version("0198f0a2-0000-7000-8000-0000000000a3", {
  effectiveDate: "2026-01-12",
  url: "https://starter.example/terms/2026-01",
  requiresAcceptance: false,
  sendsEmail: false,
  emailSentAt: null,
  state: TermsVersionResponseState.Replaced,
});

export const cancelled = version("0198f0a2-0000-7000-8000-0000000000a4", {
  effectiveDate: "2026-12-15",
  state: TermsVersionResponseState.Cancelled,
  cancelledAt: "2026-11-03T09:00:00Z",
});

/** GetTermsOfUse with these versions, the latest day first as the API answers. */
export const termsAnswer = (
  versions: TermsVersionResponse[] = [scheduled, inForce, replaced],
) =>
  http.get("*/v1/platform/terms", () =>
    HttpResponse.json({ versions, activeUserCount }),
  );

/** GetTermsVersion of one version. */
export const versionAnswer = (value: TermsVersionResponse) =>
  http.get(`*/v1/platform/terms/versions/${value.id}`, () =>
    HttpResponse.json({ version: value, activeUserCount }),
  );

export const person = (
  name: string,
  email: string,
  acceptedAt: string | null,
): TermsPersonResponse => ({
  userId: `0198f0a2-0000-7000-8000-${email.length.toString().padStart(4, "0")}${name.length.toString().padStart(8, "0")}`,
  name,
  email,
  status: acceptedAt
    ? TermsPersonResponseStatus.Accepted
    : TermsPersonResponseStatus.NotYet,
  acceptedAt,
});

export const accepted = [
  person("Ayşe Demir", "ayse@northwind.example", "2026-11-02T09:14:00Z"),
  person("Omar Haddad", "omar@northwind.example", "2026-11-01T17:40:00Z"),
];
export const notYet = [
  person("Aiko Tanaka", "aiko@blueharbour.example", null),
  person("Mark Ellis", "mark@pacificcoast.example", null),
];

/** ListTermsVersionPeople: one page of each tab; each request's address is recorded. */
export const peopleAnswer = (requests: URL[] = []) =>
  http.get("*/v1/platform/terms/versions/:versionId/people", ({ request }) => {
    const url = new URL(request.url);
    requests.push(url);
    const page: CursorPageOfTermsPersonResponse = {
      items:
        url.searchParams.get("status") === TermsPersonResponseStatus.NotYet
          ? notYet
          : accepted,
      nextCursor: null,
    };
    return HttpResponse.json(page);
  });
