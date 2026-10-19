"use client";

import { useCallback, useState } from "react";

/**
 * One Idempotency-Key per intent (F48): a new key when the hook mounts, the same key for a second click and for a retry after
 * a network error, and a new key from `renew` when the intent changes or succeeds.
 */
export function useIntentKey() {
  const [key, setKey] = useState(() => crypto.randomUUID());
  const renew = useCallback(() => setKey(crypto.randomUUID()), []);
  return { key, renew };
}
