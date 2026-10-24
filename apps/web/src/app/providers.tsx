"use client";

import { configureApiClient } from "@repo/api-client";
import { Toaster } from "@repo/ui/components/toast";
import { TooltipProvider } from "@repo/ui/components/ui/tooltip";
import type { Direction } from "@repo/ui/lib/locale";
import { zodErrorMap } from "@repo/ui/lib/zod-error-map";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { ThemeProvider } from "next-themes";
import { NuqsAdapter } from "nuqs/adapters/next/app";
import { type ReactNode, useState } from "react";
import { z } from "zod";
import { retryUnlessAccountBlocked } from "@/shared/problem";

// The browser calls the API only through the BFF on this origin (F6). The one Zod error map gives message keys (F12).
configureApiClient({ baseUrl: "/api" });
z.config({ customError: zodErrorMap });

/** The client providers of the root layout: theme, server data, URL state, tooltips and toasts (F8, F9, F23, F24). */
export function Providers({
  dir,
  children,
}: {
  dir: Direction;
  children: ReactNode;
}) {
  const t = useTranslations("common");
  // One QueryClient per browser, made once (TanStack Query, "Advanced Server Rendering"). A suspended or deleted user's answer is
  // final, so it is not retried (A9).
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: { staleTime: 60_000, retry: retryUnlessAccountBlocked },
        },
      }),
  );
  return (
    <ThemeProvider
      attribute="class"
      defaultTheme="system"
      enableSystem
      enableColorScheme
      disableTransitionOnChange
    >
      <QueryClientProvider client={queryClient}>
        <NuqsAdapter>
          <TooltipProvider delay={300}>{children}</TooltipProvider>
          <Toaster
            dir={dir}
            closeLabel={t("close")}
            regionLabel={t("notifications")}
          />
        </NuqsAdapter>
      </QueryClientProvider>
    </ThemeProvider>
  );
}
