"use client";

import { serverReach } from "@repo/api-client";
import { brand } from "@repo/brand";
import { ConnectionBar } from "@repo/ui/components/connection-bar";
import { useQueryClient } from "@tanstack/react-query";
import { useSyncExternalStore } from "react";

/**
 * The shell's connection bar (the Connection bar): it follows the app's own calls through the client's caches (`serverReach`), so
 * it sends no request of its own. A try reads the page's queries again.
 */
export function ServerBar() {
  const queryClient = useQueryClient();
  const { serverDown, noAnswer } = useServerReach();
  return (
    <ConnectionBar
      serverDown={serverDown}
      noAnswer={noAnswer}
      product={brand.name}
      statusUrl={brand.statusUrl}
      onRetry={() => void queryClient.invalidateQueries()}
    />
  );
}

/** The verdict of the app's own calls, which the bar and the bell share (`serverReach`, F59). */
export function useServerReach() {
  const reach = serverReach(useQueryClient());
  const serverDown = useSyncExternalStore(
    reach.subscribe,
    reach.isDown,
    () => false,
  );
  const noAnswer = useSyncExternalStore(
    reach.subscribe,
    reach.noAnswer,
    () => false,
  );
  return { serverDown, noAnswer };
}
