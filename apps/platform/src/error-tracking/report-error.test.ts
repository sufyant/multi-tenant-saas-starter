import { beforeEach, describe, expect, it, vi } from "vitest";
import { reportBrowserError, reportError } from "./report-error";

// The SDK is faked here, as the API is faked at the network: the test checks what goes to it, and nothing leaves.
const sdk = vi.hoisted(() => ({
  captureException: vi.fn(),
  captureRequestError: vi.fn(),
}));
vi.mock("@sentry/nextjs", () => sdk);

beforeEach(() => {
  vi.clearAllMocks();
});

describe("reportError", () => {
  it("ReportError_AnyError_GivesItToTheSdk", () => {
    const error = new Error("broken");

    reportError(error);

    expect(sdk.captureException).toHaveBeenCalledWith(error);
  });
});

// The error pages: a browser error goes out; a server error comes with only its digest, and onRequestError sent it (F55).
describe("reportBrowserError", () => {
  it("ReportBrowserError_BrowserError_SendsIt", () => {
    const error = new TypeError("x is undefined");

    reportBrowserError(error);

    expect(sdk.captureException).toHaveBeenCalledWith(error);
  });

  it("ReportBrowserError_ServerErrorWithDigest_SendsNothing", () => {
    reportBrowserError(Object.assign(new Error(""), { digest: "2847501833" }));

    expect(sdk.captureException).not.toHaveBeenCalled();
  });
});
