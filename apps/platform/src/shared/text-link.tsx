import Link from "next/link";
import type { ReactNode } from "react";

/**
 * A link inside running text. The underline marks it, so its colour is not the only signal (WCAG 2.2, 1.4.1 "Use of Color"; axe
 * `link-in-text-block`). Pass it to `t.rich`: `link: (chunks) => <TextLink href={href}>{chunks}</TextLink>`.
 */
export function TextLink({
  href,
  children,
}: {
  href: string;
  children: ReactNode;
}) {
  return (
    <Link
      href={href}
      className="text-primary underline underline-offset-4 hover:text-primary-hover"
    >
      {children}
    </Link>
  );
}
