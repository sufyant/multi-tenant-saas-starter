import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { FakeEventSource } from "../../test/event-source";
import { useLiveSignal } from "./use-live-signal";

const url = "/api/v1/me/notifications/stream";
const latest = () => FakeEventSource.latest();

describe("useLiveSignal", () => {
  const onSignal = vi.fn();
  const onResync = vi.fn();
  const live = () =>
    renderHook(() =>
      useLiveSignal({ url, event: "notifications", onSignal, onResync }),
    );

  beforeEach(() => {
    FakeEventSource.opened = [];
    vi.clearAllMocks();
    vi.stubGlobal("EventSource", FakeEventSource);
  });
  afterEach(() => vi.unstubAllGlobals());

  it("UseLiveSignal_Mounted_OpensOneStreamOnTheUrl", () => {
    live();

    expect(FakeEventSource.opened).toHaveLength(1);
    expect(latest().url).toBe(url);
  });

  it("UseLiveSignal_AnEvent_GivesItsData", () => {
    live();

    act(() => {
      latest().open();
      latest().send("notifications", '{"scope":"personal"}');
    });

    expect(onSignal).toHaveBeenCalledExactlyOnceWith('{"scope":"personal"}');
  });

  it("UseLiveSignal_AnotherEvent_GivesNothing", () => {
    live();

    act(() => latest().send("message", "x"));

    expect(onSignal).not.toHaveBeenCalled();
  });

  it("UseLiveSignal_FirstOpen_ReadsNothingAgain", () => {
    live();

    act(() => latest().open());

    expect(onResync).not.toHaveBeenCalled();
  });

  it("UseLiveSignal_StreamOpensAgain_ReadsAgain", () => {
    live();

    act(() => {
      latest().open();
      latest().open();
    });

    expect(onResync).toHaveBeenCalledOnce();
  });

  it.each(["focus", "online"])("UseLiveSignal_%s_ReadsAgain", (type) => {
    live();

    act(() => {
      window.dispatchEvent(new Event(type));
    });

    expect(onResync).toHaveBeenCalledOnce();
  });

  it.each(["focus", "online"])(
    "UseLiveSignal_ClosedStreamAnd%s_OpensANewStream",
    (type) => {
      live();

      act(() => {
        latest().fail();
        window.dispatchEvent(new Event(type));
      });

      expect(FakeEventSource.opened).toHaveLength(2);
    },
  );

  it("UseLiveSignal_OpenStreamAndFocus_KeepsTheStream", () => {
    live();

    act(() => {
      latest().open();
      window.dispatchEvent(new Event("focus"));
    });

    expect(FakeEventSource.opened).toHaveLength(1);
  });

  it("UseLiveSignal_Unmounted_ClosesTheStream", () => {
    const { unmount } = live();

    unmount();

    expect(latest().closed).toBe(true);
  });

  it("UseLiveSignal_NoEventSource_StillReadsAgainOnFocus", () => {
    vi.stubGlobal("EventSource", undefined);
    live();

    act(() => {
      window.dispatchEvent(new Event("focus"));
    });

    expect(onResync).toHaveBeenCalledOnce();
  });
});
