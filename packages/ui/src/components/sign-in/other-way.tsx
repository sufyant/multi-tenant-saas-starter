"use client";

import { useTranslations } from "next-intl";

/**
 * The line under the first step that leads to the other of sign-in and sign-up: "New here? Create an account" (frame 1.24) and
 * "Already have an account? Sign in" (1.25).
 */
export function OtherWay({
  message,
  href,
}: {
  message: "newHere" | "haveAccount";
  href: string;
}) {
  const t = useTranslations("signIn");
  return (
    <p className="text-center text-small text-muted-foreground">
      {t.rich(message, {
        link: (words) => (
          <a
            href={href}
            className="rounded-sm text-link underline underline-offset-4 outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {words}
          </a>
        ),
      })}
    </p>
  );
}
