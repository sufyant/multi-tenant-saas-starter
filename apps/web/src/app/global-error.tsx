"use client";

import { useEffect } from "react";
import { messages } from "@/i18n/messages";
import "./globals.css";

// The root layout itself failed, so no provider runs. The page reads the messages of the document's language directly (F14).
export default function GlobalError({
  error,
  retry,
}: {
  error: Error & { digest?: string };
  retry: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);
  const language =
    typeof document === "undefined" ? undefined : document.documentElement.lang;
  const text =
    messages[
      (language && language in messages
        ? language
        : Object.keys(messages)[0]) as keyof typeof messages
    ];
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
        <title>{text.problems.errorTitle}</title>
        <h1 className="text-page-title">{text.problems.errorTitle}</h1>
        <p className="text-body text-muted-foreground">
          {text.problems.errorText}
        </p>
        <button
          type="button"
          onClick={() => retry()}
          className="mt-4 h-control-md rounded-md bg-primary px-3 text-body font-medium text-primary-foreground"
        >
          {text.common.tryAgain}
        </button>
      </body>
    </html>
  );
}
