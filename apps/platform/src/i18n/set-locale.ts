"use server";

import { Language } from "@repo/api-client";
import { localeCookie } from "@repo/ui/lib/locale";
import { cookies } from "next/headers";

const oneYear = 60 * 60 * 24 * 365;

/** Keeps the user's language in a cookie, so the server renders the next page in it (F13). */
export async function setLocale(language: Language): Promise<void> {
  if (!Object.values(Language).includes(language)) return;
  (await cookies()).set(localeCookie, language, {
    path: "/",
    maxAge: oneYear,
    sameSite: "lax",
    secure: true,
  });
}
