import { Language } from "@repo/api-client";
import { chooseLocale, localeCookie } from "@repo/ui/lib/locale";
import { pseudoCookie, pseudoMessages } from "@repo/ui/lib/pseudo-locale";
import { cookies, headers } from "next/headers";
import { getRequestConfig } from "next-intl/server";
import { messages } from "./messages";

// next-intl without i18n routing (F13): the cookie set from GetMe.language, else Accept-Language, else English.
// Playwright's test locale (the Long text pattern): a server started with E2E_PSEUDO_LOCALE=1 turns every text 40% longer, with
// accents, for a request with the cookie of `pseudoCookie`. No other run sets the variable.
export default getRequestConfig(async () => {
  const jar = await cookies();
  const locale = chooseLocale(
    jar.get(localeCookie)?.value,
    (await headers()).get("accept-language"),
    Object.values(Language),
    Language.En,
  );
  const pseudo = process.env.E2E_PSEUDO_LOCALE === "1" && jar.has(pseudoCookie);
  return {
    locale,
    messages: pseudo ? pseudoMessages(messages[locale]) : messages[locale],
  };
});
