"use client";

import { useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { cn } from "../../lib/utils";
import { CheckIcon, CopyIcon, Icon } from "../icon";

/** How long "Copied" shows after a click (the Copy pattern). */
export const copiedMs = 2000;

export type CopyButtonProps = {
  /** The text that goes to the clipboard: an address, an email, a version. */
  value: string;
  /** The button's accessible name, which starts with its label "Copy" (WCAG 2.2, 2.5.3): "Copy the address". */
  label: string;
  className?: string;
};

/**
 * The small copy button beside an address or an email (the Copy pattern): its icon and "Copy". After a click it says "Copied"
 * for two seconds; a polite live region says it to a screen reader too.
 */
export function CopyButton({ value, label, className }: CopyButtonProps) {
  const t = useTranslations("patterns");
  const [copied, setCopied] = useState(false);
  useEffect(() => {
    if (!copied) return;
    const timer = setTimeout(() => setCopied(false), copiedMs);
    return () => clearTimeout(timer);
  }, [copied]);
  return (
    <>
      <button
        type="button"
        aria-label={label}
        onClick={() =>
          void navigator.clipboard.writeText(value).then(() => setCopied(true))
        }
        className={cn(
          "inline-flex h-control-sm shrink-0 items-center gap-1 rounded-md px-1.5 text-small text-muted-foreground outline-none hover:bg-accent hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring pointer-coarse:h-touch-target",
          className,
        )}
      >
        <Icon icon={copied ? CheckIcon : CopyIcon} size={14} />
        <span aria-hidden="true">{copied ? t("copied") : t("copy")}</span>
      </button>
      <span role="status" className="sr-only">
        {copied ? t("copied") : ""}
      </span>
    </>
  );
}
