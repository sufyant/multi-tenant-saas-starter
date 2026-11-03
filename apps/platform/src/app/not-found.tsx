import { auth } from "@clerk/nextjs/server";
import { brand } from "@repo/brand";
import { AuthFooter, AuthLayout } from "@repo/ui/components/auth-layout";
import { NotFound } from "@repo/ui/components/not-found";
import { getTranslations } from "next-intl/server";
import { setLocale } from "@/i18n/set-locale";

// A page that no route has (the Page not found screens). Signed out, it may be a page that needs a session, so it offers sign-in
// in the frame before sign-in; signed in, it goes home.
export default async function NotFoundPage() {
  if (!(await auth()).isAuthenticated) {
    const t = await getTranslations();
    return (
      <AuthLayout
        brand={t("meta.title")}
        platformLabel={t("shell.platform")}
        footer={
          <AuthFooter
            onLanguageChange={setLocale}
            privacyUrl={brand.privacyUrl}
          />
        }
      >
        <NotFound kind="signed-out" href="/sign-in" />
      </AuthLayout>
    );
  }
  return (
    <main className="mx-auto flex min-h-dvh max-w-xl items-center p-6">
      <NotFound kind="page" href="/" />
    </main>
  );
}
