import { ProblemDetailsError } from "@repo/api-client";
import type { ErrorEvent, EventHint } from "@sentry/nextjs";
import { getDefaultIntegrations } from "@sentry/nextjs";
import { describe, expect, it } from "vitest";
import { browserOptions, serverOptions } from "./options";

const tracking = {
  dsn: "https://public@errors.example.test/1",
  environment: "staging",
} as const;

// Vitest loads the SDK's Node build, so these are the server's default integrations.
const serverDefaults = getDefaultIntegrations({});
const names = (integrations: { name: string }[]) =>
  integrations.map((integration) => integration.name).sort();
const select = (
  integrations: unknown,
  defaults: { name: string }[],
): { name: string }[] =>
  (integrations as (defaults: { name: string }[]) => { name: string }[])(
    defaults,
  );

const anEvent = (value: string): ErrorEvent => ({
  type: undefined,
  exception: { values: [{ type: "Error", value }] },
});

describe("serverOptions", () => {
  it("ServerOptions_Integrations_KeepOnlyErrorsAndBreadcrumbs", () => {
    expect(
      names(select(serverOptions(tracking).integrations, serverDefaults)),
    ).toEqual([
      "Console",
      "Context",
      "Dedupe",
      "EventFilters",
      "FunctionToString",
      "Http",
      "LinkedErrors",
      "NodeFetch",
      "NodeSystemError",
      "OnUncaughtException",
      "OnUnhandledRejection",
      "RequestData",
    ]);
  });

  it("ServerOptions_TheRest_SendNoTraceSessionOrPersonalData", () => {
    const options = serverOptions(tracking);

    expect(options.tracesSampleRate).toBeUndefined();
    expect(options.sendClientReports).toBe(false);
    expect(options.dataCollection).toEqual({
      userInfo: false,
      cookies: false,
      httpHeaders: false,
      httpBodies: [],
      urlQueryParams: false,
      graphQL: { document: false, variables: false },
      genAI: { inputs: false, outputs: false },
      mcp: { inputs: false, outputs: false },
      databaseQueryData: false,
      queues: false,
      stackFrameVariables: false,
      frameContextLines: 0,
    });
    expect(options.environment).toBe("staging");
  });
});

describe("beforeSend", () => {
  const beforeSend = browserOptions(tracking).beforeSend as (
    event: ErrorEvent,
    hint: EventHint,
  ) => Promise<ErrorEvent | null>;

  it("BeforeSend_ErrorOfTheApp_SendsItWithoutTheAddress", async () => {
    const error = new Error("No member ali@northwind.io");

    const sent = await beforeSend(anEvent(error.message), {
      originalException: error,
    });

    expect(sent?.exception?.values?.[0]?.value).toBe("No member [email]");
  });

  it("BeforeSend_AnswerOfTheApi_SendsNothing", async () => {
    const error = new ProblemDetailsError(409, null);

    expect(
      await beforeSend(anEvent(error.message), { originalException: error }),
    ).toBeNull();
  });

  it("BeforeBreadcrumb_Navigation_HasNoToken", () => {
    const beforeBreadcrumb = serverOptions(tracking).beforeBreadcrumb as (
      breadcrumb: object,
    ) => object;

    expect(
      beforeBreadcrumb({
        category: "navigation",
        data: { from: "/", to: "/invitations/accept?code=inv_8Kq2Zr7Tm1" },
      }),
    ).toEqual({
      category: "navigation",
      data: { from: "/", to: "/invitations/accept?code=[token]" },
    });
  });
});
