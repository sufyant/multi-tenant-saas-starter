import { EmptyState } from "@repo/ui/components/empty-state";
import Link from "next/link";
import { useTranslations } from "next-intl";

/** "Page not found": the root's not-found page, and an organisation the API does not know (404 `tenant.not_found`). */
export function NotFoundPage() {
  const t = useTranslations();
  return (
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
  );
}
