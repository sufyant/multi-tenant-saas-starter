import { Language } from "@repo/api-client";
import { chooseLocale, localeCookie } from "@repo/ui/lib/locale";
import { cookies, headers } from "next/headers";
import { getRequestConfig } from "next-intl/server";
import { messages } from "./messages";

// next-intl without i18n routing (F13): the cookie set from GetMe.language, else Accept-Language, else English.
export default getRequestConfig(async () => {
  const locale = chooseLocale(
    (await cookies()).get(localeCookie)?.value,
    (await headers()).get("accept-language"),
    Object.values(Language),
    Language.En,
  );
  return { locale, messages: messages[locale] };
});
