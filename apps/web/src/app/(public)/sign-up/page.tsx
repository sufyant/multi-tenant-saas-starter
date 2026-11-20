import { auth } from "@clerk/nextjs/server";
import { brand } from "@repo/brand";
import { redirect } from "next/navigation";
import { getTranslations } from "next-intl/server";
import { SignUpScreen } from "@/features/sign-in/sign-up-screen";
import { setLocale } from "@/i18n/set-locale";

// Our own sign-up while the product has sign-up on (F41, 40d): packages/ui's SignUp with the app's error tracking (F55). A signed-in
// person has nothing to do here.
export default async function SignUpPage() {
  if ((await auth()).isAuthenticated) redirect("/");
  const t = await getTranslations();
  return (
    <SignUpScreen
      title={t("signUpPage.title")}
      text={t("signUpPage.text")}
      brand={t("meta.title")}
      onLanguageChange={setLocale}
      privacyUrl={brand.privacyUrl}
    />
  );
}
