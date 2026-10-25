import type { ErrorEvent } from "@sentry/nextjs";
import { describe, expect, it } from "vitest";
import { scrubBreadcrumb, scrubEvent, scrubText } from "./privacy";

// Sample values, never real ones: the design system's sample organisation and made-up tokens.
const address = "ali@northwind.io";
const code = "inv_8Kq2Zr7Tm1";
const ticket = "eyJhbGciOiJSUzI1NiJ9.ticket";

const event = (fields: Partial<ErrorEvent>): ErrorEvent => ({
  type: undefined,
  ...fields,
});

describe("scrubText", () => {
  it.each([
    ["an address", `No member ${address}.`, "No member [email]."],
    [
      "an address with a plus",
      "ali+clerk_test@northwind.io left",
      "[email] left",
    ],
    [
      "an address in a query",
      "/v1/platform/users?search=ali%40northwind.io&page=2",
      "/v1/platform/users?search=[email]&page=2",
    ],
    [
      "an encoded plus in a query",
      "/users?search=ali%2Bclerk_test%40northwind.io",
      "/users?search=[email]",
    ],
    [
      "the invitation's code",
      `https://web.example.test/invitations/accept?code=${code}`,
      "https://web.example.test/invitations/accept?code=[token]",
    ],
    [
      "the code after Clerk's ticket",
      `/invitations/accept?__clerk_ticket=${ticket}&code=${code}`,
      "/invitations/accept?__clerk_ticket=[token]&code=[token]",
    ],
    [
      "Clerk's ticket",
      `/sign-in?__clerk_ticket=${ticket}`,
      "/sign-in?__clerk_ticket=[token]",
    ],
    [
      "Clerk's development browser token",
      "/settings?tab=roles&__clerk_db_jwt=dvb_2abc",
      "/settings?tab=roles&__clerk_db_jwt=[token]",
    ],
    [
      "Clerk's handshake values",
      "/?__clerk_handshake=abc.def&__clerk_handshake_nonce=n1",
      "/?__clerk_handshake=[token]&__clerk_handshake_nonce=[token]",
    ],
  ])("ScrubText_%s_IsReplaced", (_, text, expected) => {
    expect(scrubText(text)).toBe(expected);
  });

  it.each([
    ["a code outside the accept page", "/sso-callback?code=4f2a"],
    ["another query value", "/members?roleId=r_1&page=3"],
    ["a sentence without an address", "Cannot read properties of undefined"],
  ])("ScrubText_%s_StaysAsItIs", (_, text) => {
    expect(scrubText(text)).toBe(text);
  });
});

describe("scrubEvent", () => {
  it("ScrubEvent_Message_HasNoAddress", () => {
    expect(scrubEvent(event({ message: `Invite ${address}` })).message).toBe(
      "Invite [email]",
    );
  });

  it("ScrubEvent_LogEntry_HasNoAddressInMessageOrParameters", () => {
    const scrubbed = scrubEvent(
      event({ logentry: { message: `To ${address}`, params: [address, 2] } }),
    );

    expect(scrubbed.logentry).toEqual({
      message: "To [email]",
      params: ["[email]", 2],
    });
  });

  it("ScrubEvent_ExceptionValue_HasNoAddressOrCode", () => {
    const scrubbed = scrubEvent(
      event({
        exception: {
          values: [
            { type: "Error", value: `Failed for ${address}` },
            { type: "Error", value: `GET /invitations/accept?code=${code}` },
          ],
        },
      }),
    );

    expect(scrubbed.exception?.values?.map((value) => value.value)).toEqual([
      "Failed for [email]",
      "GET /invitations/accept?code=[token]",
    ]);
  });

  it("ScrubEvent_RequestUrl_HasNoCodeOrTicket", () => {
    const scrubbed = scrubEvent(
      event({
        request: {
          url: `https://web.example.test/invitations/accept?code=${code}&__clerk_ticket=${ticket}`,
        },
      }),
    );

    expect(scrubbed.request?.url).toBe(
      "https://web.example.test/invitations/accept?code=[token]&__clerk_ticket=[token]",
    );
  });

  it.each([
    [
      "a string",
      `code=${code}&__clerk_ticket=${ticket}&lang=tr`,
      "code=[token]&__clerk_ticket=[token]&lang=tr",
    ],
    [
      "pairs",
      [
        ["code", code],
        ["lang", "tr"],
      ],
      [
        ["code", "[token]"],
        ["lang", "tr"],
      ],
    ],
    [
      "an object",
      { code, __clerk_db_jwt: "dvb_2abc" },
      { code: "[token]", __clerk_db_jwt: "[token]" },
    ],
  ])(
    "ScrubEvent_QueryStringAsOf%s_OnTheAcceptPage_HasNoToken",
    (_, query, expected) => {
      const scrubbed = scrubEvent(
        event({
          request: {
            url: "https://web.example.test/invitations/accept",
            query_string: query as never,
          },
        }),
      );

      expect(scrubbed.request?.query_string).toEqual(expected);
    },
  );

  it("ScrubEvent_QueryStringOfAnotherPage_KeepsItsCode", () => {
    const scrubbed = scrubEvent(
      event({
        request: {
          url: "https://web.example.test/sso-callback",
          query_string: "code=4f2a&__clerk_db_jwt=dvb_2abc",
        },
      }),
    );

    expect(scrubbed.request?.query_string).toBe(
      "code=4f2a&__clerk_db_jwt=[token]",
    );
  });

  it("ScrubEvent_Breadcrumbs_HaveNoAddressOrToken", () => {
    const scrubbed = scrubEvent(
      event({
        breadcrumbs: [
          { category: "console", message: `Sent to ${address}` },
          {
            category: "navigation",
            data: {
              from: `/invitations/accept?code=${code}`,
              to: `/sign-in?__clerk_ticket=${ticket}`,
            },
          },
        ],
      }),
    );

    expect(scrubbed.breadcrumbs).toEqual([
      { category: "console", message: "Sent to [email]" },
      {
        category: "navigation",
        data: {
          from: "/invitations/accept?code=[token]",
          to: "/sign-in?__clerk_ticket=[token]",
        },
      },
    ]);
  });
});

describe("scrubBreadcrumb", () => {
  it("ScrubBreadcrumb_Message_HasNoAddress", () => {
    expect(
      scrubBreadcrumb({ category: "console", message: `Hello ${address}` })
        .message,
    ).toBe("Hello [email]");
  });

  it("ScrubBreadcrumb_FetchUrl_HasNoAddress", () => {
    const scrubbed = scrubBreadcrumb({
      category: "fetch",
      data: {
        method: "GET",
        url: "/api/v1/platform/users?search=ali%40northwind.io",
        status_code: 200,
      },
    });

    expect(scrubbed.data).toEqual({
      method: "GET",
      url: "/api/v1/platform/users?search=[email]",
      status_code: 200,
    });
  });

  it("ScrubBreadcrumb_NavigationFromAndTo_HaveNoToken", () => {
    const scrubbed = scrubBreadcrumb({
      category: "navigation",
      data: {
        from: `/invitations/accept?code=${code}`,
        to: "/?__clerk_db_jwt=dvb_2abc",
      },
    });

    expect(scrubbed.data).toEqual({
      from: "/invitations/accept?code=[token]",
      to: "/?__clerk_db_jwt=[token]",
    });
  });
});
