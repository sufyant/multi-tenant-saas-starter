import { EmptyState } from "@repo/ui/components/empty-state";
import Link from "next/link";
import { getTranslations } from "next-intl/server";

export default async function NotFound() {
  const t = await getTranslations();
  return (
    <main className="mx-auto flex min-h-dvh max-w-xl items-center p-6">
      <EmptyState
        title={t("problems.notFoundTitle")}
        description={t("problems.notFoundText")}
        action={
          <Link
            href="/"
            className="text-primary underline-offset-4 hover:underline"
          >
            {t("common.goHome")}
          </Link>
        }
      />
    </main>
  );
}
