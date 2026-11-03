import { auth } from "@clerk/nextjs/server";
import { brand } from "@repo/brand";
import { AuthFooter, AuthLayout } from "@repo/ui/components/auth-layout";
import { NotFound } from "@repo/ui/components/not-found";
import { getTranslations } from "next-intl/server";
import { PlatformShell } from "@/features/shell/platform-shell";
import { setLocale } from "@/i18n/set-locale";

// A page that no route has (the Page not found screens). Signed out, it may be a page that needs a session, so it offers sign-in
// in the frame before sign-in; signed in, it shows inside the shell, so the menu stays, and Organisations is the way on. The
// [...missing] page that ends here is outside the (app) group: the shell draws its page only once GetMe names a system admin, which
// the server does not know, so only outside it does notFound() come before the response starts, and the answer is a 404 (F59).
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
    <PlatformShell>
      <NotFound kind="page" href="/" />
    </PlatformShell>
  );
}
