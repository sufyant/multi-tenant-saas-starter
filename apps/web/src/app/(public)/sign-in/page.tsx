import { auth } from "@clerk/nextjs/server";
import { SignIn } from "@repo/ui/components/sign-in";
import { redirect } from "next/navigation";
import { getTranslations } from "next-intl/server";
import { setLocale } from "@/i18n/set-locale";

// Our own sign-in (F41): packages/ui's SignIn, with the title of this app. A signed-in person has nothing to do here.
export default async function SignInPage() {
  if ((await auth()).isAuthenticated) redirect("/");
  const t = await getTranslations();
  return (
    <SignIn
      title={t("signInPage.title")}
      brand={t("meta.title")}
      onLanguageChange={setLocale}
    />
  );
}
