"use client";

import { createContext, type ReactNode, useContext } from "react";
import type { Environment } from "../../lib/environment";

export type AppFrame = {
  /** The environment of `APP_ENVIRONMENT` (F7). */
  environment: Environment;
  /** The image's commit, seven characters; none in a run without `SOURCE_REVISION`. */
  version?: string;
};

const Context = createContext<AppFrame>({
  environment: { kind: "production" },
});

/** What the server read at start for the frame of every page: the environment and the version. The root layout gives it. */
export function AppFrameProvider({
  value,
  children,
}: {
  value: AppFrame;
  children: ReactNode;
}) {
  return <Context value={value}>{children}</Context>;
}

export const useAppFrame = () => useContext(Context);
