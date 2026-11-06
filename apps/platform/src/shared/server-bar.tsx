"use client";

import { serverReach } from "@repo/api-client";
import { brand } from "@repo/brand";
import { ConnectionBar } from "@repo/ui/components/connection-bar";
import { useQueryClient } from "@tanstack/react-query";
import { useMemo, useSyncExternalStore } from "react";

/**
 * The shell's connection bar (the Connection bar): it follows the app's own calls through the client's caches (`serverReach`), so
 * it sends no request of its own. A try reads the page's queries again.
 */
export function ServerBar() {
  const queryClient = useQueryClient();
  const reach = useMemo(() => serverReach(queryClient), [queryClient]);
  const serverDown = useSyncExternalStore(
    reach.subscribe,
    reach.isDown,
    () => false,
  );
  return (
    <ConnectionBar
      serverDown={serverDown}
      product={brand.name}
      statusUrl={brand.statusUrl}
      onRetry={() => void queryClient.invalidateQueries()}
    />
  );
}
