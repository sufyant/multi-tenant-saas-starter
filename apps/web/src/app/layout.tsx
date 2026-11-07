import { ClerkProvider } from "@clerk/nextjs";
import { shadcn } from "@clerk/ui/themes";
import { environmentOf, environmentTexts } from "@repo/ui/lib/environment";
import { directionOf } from "@repo/ui/lib/locale";
import { shortRevision } from "@repo/ui/lib/support-link";
import type { Metadata, Viewport } from "next";
import localFont from "next/font/local";
import { NextIntlClientProvider } from "next-intl";
import { getLocale, getTranslations } from "next-intl/server";
import { env } from "@/env";
import { Providers } from "./providers";
import "./globals.css";

// The fonts come from their Fontsource packages, so the lockfile pins them and the build makes no network request; next/font
// serves them from this origin (F19). Each is the variable file of the subsets next/font/google loaded before. Instrument Sans has
// two files and next/font/local takes one unicode-range per call, so the two faces share every range: the browser tries the last
// face first and takes from the first only the glyphs that the last does not have (CSS Fonts, "unicode-range").
const instrumentSans = localFont({
  variable: "--font-instrument-sans",
  src: [
    {
      path: "../../node_modules/@fontsource-variable/instrument-sans/files/instrument-sans-latin-ext-wght-normal.woff2",
    },
    {
      path: "../../node_modules/@fontsource-variable/instrument-sans/files/instrument-sans-latin-wght-normal.woff2",
    },
  ],
  weight: "400 700",
});
const jetBrainsMono = localFont({
  variable: "--font-jetbrains-mono",
  src: "../../node_modules/@fontsource-variable/jetbrains-mono/files/jetbrains-mono-latin-wght-normal.woff2",
  weight: "100 800",
});
// The Arabic subset's own range (the package's wght.css), so a digit or a Latin word still takes Instrument Sans.
const notoArabic = localFont({
  variable: "--font-noto-arabic",
  src: "../../node_modules/@fontsource-variable/noto-sans-arabic/files/noto-sans-arabic-arabic-wght-normal.woff2",
  weight: "400 600",
  declarations: [
    {
      prop: "unicode-range",
      value:
        "U+0600-06FF,U+0750-077F,U+0870-088E,U+0890-0891,U+0897-08E1,U+08E3-08FF,U+200C-200E,U+2010-2011,U+204F,U+2E41,U+FB50-FDFF,U+FE70-FE74,U+FE76-FEFC,U+102E0-102FB,U+10E60-10E7E,U+10EC2-10EC4,U+10EFC-10EFF,U+1EE00-1EE03,U+1EE05-1EE1F,U+1EE21-1EE22,U+1EE24,U+1EE27,U+1EE29-1EE32,U+1EE34-1EE37,U+1EE39,U+1EE3B,U+1EE42,U+1EE47,U+1EE49,U+1EE4B,U+1EE4D-1EE4F,U+1EE51-1EE52,U+1EE54,U+1EE57,U+1EE59,U+1EE5B,U+1EE5D,U+1EE5F,U+1EE61-1EE62,U+1EE64,U+1EE67-1EE6A,U+1EE6C-1EE72,U+1EE74-1EE77,U+1EE79-1EE7C,U+1EE7E,U+1EE80-1EE89,U+1EE8B-1EE9B,U+1EEA1-1EEA3,U+1EEA5-1EEA9,U+1EEAB-1EEBB,U+1EEF0-1EEF1",
    },
  ],
});

// The tab's title and icon (the Browser tab screens): "Sign in · Starter"; outside production its environment comes first and the
// icon is amber. A signed-in page then writes its own title in the browser, with the unread count (useTabTitle).
export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations();
  const environment = environmentOf(env().APP_ENVIRONMENT);
  const texts = environmentTexts(environment);
  const mark = texts
    ? `${t(`appShell.environment.${texts.tab}`, texts.values)} `
    : "";
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
    WEB_APP_URL,
  } = env();
  return (
    <html
      lang={locale}
      dir={dir}
      className={`${instrumentSans.variable} ${jetBrainsMono.variable} ${notoArabic.variable} h-full antialiased`}
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
                webAppUrl: WEB_APP_URL,
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
