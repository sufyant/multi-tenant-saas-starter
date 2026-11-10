import { ArrowLeftIcon, Icon } from "@repo/ui/components/icon";
import { buttonVariants } from "@repo/ui/components/ui/button";
import Link from "next/link";

/** The way back to a record's list above its page (5.05, 10.15): a link, drawn as the frame's ghost button, because it navigates. */
export function BackLink({ href, label }: { href: string; label: string }) {
  return (
    <div>
      <Link href={href} className={buttonVariants({ variant: "ghost" })}>
        <Icon icon={ArrowLeftIcon} size={16} className="rtl:rotate-180" />
        {label}
      </Link>
    </div>
  );
}
