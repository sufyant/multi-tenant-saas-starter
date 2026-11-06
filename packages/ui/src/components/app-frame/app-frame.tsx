"use client";

import { createContext, type ReactNode, useContext } from "react";
import type { Environment } from "../../lib/environment";
import { cn } from "../../lib/utils";
import { EnvironmentStrip } from "../environment-strip";

export type AppFrame = {
  /** The environment of `APP_ENVIRONMENT` (F7). */
  environment: Environment;
  /** The image's commit, seven characters; none in a run without `SOURCE_REVISION`. */
  version?: string;
};

const Context = createContext<AppFrame>({
  environment: { kind: "production" },
});

/**
 * What the server read at start for the frame of every page, the environment and the version, and the environment strip above
 * every page outside production (F59). The strip's height is `--strip-height`, so a full-height frame below it, such as the
 * shell's sidebar, leaves it its place. The root layout gives it.
 */
export function AppFrameProvider({
  value,
  children,
}: {
  value: AppFrame;
  children: ReactNode;
}) {
  const strip = value.environment.kind !== "production";
  return (
    <Context value={value}>
      <div
        className={cn(
          "flex min-h-dvh flex-col",
          strip
            ? "[--strip-height:calc(28px+env(safe-area-inset-top))] tablet:[--strip-height:24px]"
            : "[--strip-height:0px]",
        )}
      >
        <EnvironmentStrip environment={value.environment} />
        {children}
      </div>
    </Context>
  );
}

export const useAppFrame = () => useContext(Context);
