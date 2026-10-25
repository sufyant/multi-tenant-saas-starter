import { auth } from "@clerk/nextjs/server";
import { redirect } from "next/navigation";
import { getTranslations } from "next-intl/server";
import { SignInScreen } from "@/features/sign-in/sign-in-screen";
import { setLocale } from "@/i18n/set-locale";

// Our own sign-in (F41): packages/ui's SignIn with the app's error tracking (F55), with the title of this app. A signed-in person has nothing to do here.
export default async function SignInPage() {
  if ((await auth()).isAuthenticated) redirect("/");
  const t = await getTranslations();
  return (
    <SignInScreen
      title={t("signInPage.title")}
      brand={t("meta.title")}
      onLanguageChange={setLocale}
    />
  );
}
