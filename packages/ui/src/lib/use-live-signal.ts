"use client";

import { useEffect, useRef } from "react";

export type LiveSignalOptions = {
  /** The stream's address: the BFF's `/api/v1/...` (F6). */
  url: string;
  /** The name of the events that carry a signal. */
  event: string;
  /** One event's data. */
  onSignal: (data: string) => void;
  /** Read again what the signals keep current: the stream may have missed one while it was down. */
  onResync: () => void;
};

/**
 * One server-sent event stream for the tab (HTML Living Standard, "Server-sent events"). The browser opens the stream again when
 * the server closes it; each later open reads again, and so do the tab's focus and the browser's return online, when a closed
 * stream also opens again. Without `EventSource` only the reads on focus and online remain. There is no polling. The hook takes no
 * generated client: the app gives the address and what a signal does.
 */
export function useLiveSignal({
  url,
  event,
  onSignal,
  onResync,
}: LiveSignalOptions): void {
  const handlers = useRef({ onSignal, onResync });
  handlers.current = { onSignal, onResync };

  useEffect(() => {
    const supported = typeof EventSource === "function";
    let source: EventSource | undefined;
    const open = () => {
      const stream = new EventSource(url);
      let opened = false;
      stream.addEventListener(event, (message) =>
        handlers.current.onSignal((message as MessageEvent<string>).data),
      );
      stream.addEventListener("open", () => {
        if (opened) handlers.current.onResync();
        opened = true;
      });
      source = stream;
    };
    const resync = () => {
      handlers.current.onResync();
      if (supported && source?.readyState === EventSource.CLOSED) open();
    };
    if (supported) open();
    window.addEventListener("focus", resync);
    window.addEventListener("online", resync);
    return () => {
      window.removeEventListener("focus", resync);
      window.removeEventListener("online", resync);
      source?.close();
    };
  }, [url, event]);
}
