"use client";

import { brand } from "@repo/brand";
import { Banner } from "@repo/ui/components/banner";
import { useTranslations } from "next-intl";

/**
 * Over a limit (E4), on the page where it applies: a warning, what to do, and its one link, "Contact us", to the brand's support
 * contact (the board notes of the plan limits). The disabled action names its title and text as its reason (`reasonIdsOf`), not
 * its link.
 */
export function PlanBanner({
  id,
  title,
  text,
}: {
  id: string;
  title: string;
  text: string;
}) {
  const t = useTranslations("plans");
  return (
    <Banner
      tone="warning"
      title={<span id={`${id}-title`}>{title}</span>}
      action={
        // A link, not a button: it leaves for the support contact. Underlined, so it is not told by colour alone (WCAG 1.4.1).
        <a
          href={brand.supportUrl}
          className="shrink-0 font-medium underline underline-offset-2"
        >
          {t("contact")}
        </a>
      }
    >
      <span id={`${id}-text`}>{text}</span>
    </Banner>
  );
}

/** The ids of the banner's title and text, for the `aria-describedby` of the action it closes. */
export const reasonIdsOf = (id: string) => `${id}-title ${id}-text`;
