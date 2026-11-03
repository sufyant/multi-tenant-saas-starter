import { ClerkProvider } from "@clerk/nextjs";
import { shadcn } from "@clerk/ui/themes";
import { environmentOf } from "@repo/ui/lib/environment";
import { directionOf } from "@repo/ui/lib/locale";
import { shortRevision } from "@repo/ui/lib/support-link";
import type { Metadata, Viewport } from "next";
import { Geist, Geist_Mono, IBM_Plex_Sans_Arabic } from "next/font/google";
import { NextIntlClientProvider } from "next-intl";
import { getLocale, getTranslations } from "next-intl/server";
import { env } from "@/env";
import { Providers } from "./providers";
import "./globals.css";

// next/font downloads the fonts at build time and serves them from this origin: no request to Google at run time (F19).
const geist = Geist({
  variable: "--font-geist",
  subsets: ["latin", "latin-ext"],
});
const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});
const plexArabic = IBM_Plex_Sans_Arabic({
  variable: "--font-plex-arabic",
  subsets: ["arabic"],
  weight: ["400", "500", "600"],
});

// The tab's title and icon (the Browser tab screens): "Sign in · Starter"; outside production its environment comes first and the
// icon is amber. A signed-in page then writes its own title in the browser, with the unread count (useTabTitle).
export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations();
  const environment = environmentOf(env().APP_ENVIRONMENT);
  const mark = {
    production: "",
    staging: `${t("appShell.environment.tabStaging")} `,
    local: `${t("appShell.environment.tabLocal")} `,
    preview:
      environment.kind === "preview"
        ? `${t("appShell.environment.tabPreview", { number: environment.number })} `
        : "",
  }[environment.kind];
  return {
    title: {
      default: `${mark}${t("meta.title")}`,
      template: `${mark}%s · ${t("meta.title")}`,
    },
    description: t("meta.description"),
    icons: {
      icon:
        environment.kind === "production"
          ? "/favicon.ico"
          : "/favicon-test.svg",
    },
  };
}

// The viewport shrinks with the phone's keyboard, so a full-screen form keeps its main button above it (the Dialogs on a phone
// pattern).
export const viewport: Viewport = { interactiveWidget: "resizes-content" };

export default async function RootLayout({ children }: LayoutProps<"/">) {
  const locale = await getLocale();
  const dir = directionOf(locale);
  const {
    ERROR_TRACKING_DSN,
    ERROR_TRACKING_ENVIRONMENT,
    APP_ENVIRONMENT,
    SOURCE_REVISION,
  } = env();
  return (
    <html
      lang={locale}
      dir={dir}
      className={`${geist.variable} ${geistMono.variable} ${plexArabic.variable} h-full antialiased`}
      suppressHydrationWarning
      // The browser's error tracking reads these before any script runs: the server's values at run time, not the build's (F55).
      data-error-tracking-dsn={ERROR_TRACKING_DSN}
      data-error-tracking-environment={ERROR_TRACKING_ENVIRONMENT}
    >
      <body className="min-h-full">
        {/* The key comes from the server at run time, not from the build (F7). */}
        <ClerkProvider
          publishableKey={env().CLERK_PUBLISHABLE_KEY}
          appearance={{ theme: shadcn }}
          signInUrl="/sign-in"
          afterSignOutUrl="/sign-in"
        >
          <NextIntlClientProvider>
            <Providers
              dir={dir}
              frame={{
                environment: environmentOf(APP_ENVIRONMENT),
                version: shortRevision(SOURCE_REVISION),
              }}
            >
              {children}
            </Providers>
          </NextIntlClientProvider>
        </ClerkProvider>
      </body>
    </html>
  );
}
