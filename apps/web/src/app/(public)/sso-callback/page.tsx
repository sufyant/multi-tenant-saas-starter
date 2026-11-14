import { brand } from "@repo/brand";
import { getTranslations } from "next-intl/server";
import { SsoCallbackScreen } from "@/features/sign-in/sso-callback-screen";
import { setLocale } from "@/i18n/set-locale";

// The end of a Google or Microsoft sign-in (F41), in the card of the screens before sign-in.
export default async function SsoCallbackPage() {
  const t = await getTranslations();
  return (
    <SsoCallbackScreen
      brand={t("meta.title")}
      onLanguageChange={setLocale}
      privacyUrl={brand.privacyUrl}
    />
  );
}
