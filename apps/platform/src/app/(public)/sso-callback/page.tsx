import { brand } from "@repo/brand";
import { SsoCallback } from "@repo/ui/components/sign-in";
import { getTranslations } from "next-intl/server";
import { setLocale } from "@/i18n/set-locale";

// The end of a Google or Microsoft sign-in (F41), in the card of the screens before sign-in.
export default async function SsoCallbackPage() {
  const t = await getTranslations();
  return (
    <SsoCallback
      brand={t("meta.title")}
      platformLabel={t("shell.platform")}
      onLanguageChange={setLocale}
      privacyUrl={brand.privacyUrl}
    />
  );
}
