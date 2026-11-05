"use client";

import { brand } from "@repo/brand";
import { useEffect } from "react";
import { reportBrowserError } from "@/error-tracking/report-error";
import { messages } from "@/i18n/messages";
import "./globals.css";

// The root layout itself failed, so no provider runs. The page reads the messages of the document's language directly (F14).
export default function GlobalError({
  error,
}: {
  error: Error & { digest?: string };
  retry: () => void;
}) {
  useEffect(() => {
    console.error(error);
    reportBrowserError(error);
  }, [error]);
  const language =
    typeof document === "undefined" ? undefined : document.documentElement.lang;
  const text =
    messages[
      (language && language in messages
        ? language
        : Object.keys(messages)[0]) as keyof typeof messages
    ];
  // The app could not start: the only full-page error (the App couldn't start screen).
  const title = text.problems.startTitle.replace("{product}", brand.name);
  return (
    <html
      lang={language}
      dir={
        typeof document === "undefined"
          ? undefined
          : document.documentElement.dir
      }
    >
      <body className="flex min-h-dvh flex-col items-center justify-center gap-2 bg-background p-6 text-center text-foreground">
        <title>{title}</title>
        <h1 className="text-page-title">{title}</h1>
        <p className="text-body text-muted-foreground">
          {text.problems.startText}
        </p>
        <button
          type="button"
          onClick={() => window.location.reload()}
          className="mt-4 h-control-md rounded-md bg-primary px-3 text-body font-medium text-primary-foreground"
        >
          {text.problems.reload}
        </button>
        <a
          href={brand.supportUrl}
          className="text-body text-link underline underline-offset-4"
        >
          {text.problems.contactSupport}
        </a>
      </body>
    </html>
  );
}
