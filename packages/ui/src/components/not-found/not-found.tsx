"use client";

import { useTranslations } from "next-intl";
import type { ElementType } from "react";
import { Button } from "../button";
import { EmptyState } from "../empty-state";
import { SearchXIcon } from "../icon";

export type NotFoundProps = {
  /**
   * page: an old link or a page that moved; organisation: an organisation's address that no organisation of the person has, with
   * the address when it is known; signed-out: a page that does not exist or needs a session (the Page not found screens).
   */
  kind: "page" | "organisation" | "signed-out";
  /** The organisation's address as the person typed it: "app.starter.example/northwnd". */
  address?: string;
  /** Home for a signed-in person, the sign-in page for a signed-out one. */
  href: string;
  /** The link element: Next.js's Link in the apps. */
  linkComponent?: ElementType;
};

/** "Page not found" and "Organisation not found" in both apps (the Page not found screens): what happened and the one way on. */
export function NotFound({
  kind,
  address,
  href,
  linkComponent: Link = "a",
}: NotFoundProps) {
  const t = useTranslations("problems");
  const organisation = kind === "organisation";
  const signedOut = kind === "signed-out";
  // Inside the shell the screen has the search-x tile and a secondary button; signed out, the sign-in button is the page's one
  // action (2.40 to 2.44).
  return (
    <EmptyState
      size={signedOut ? "list" : "page"}
      icon={signedOut ? undefined : SearchXIcon}
      title={organisation ? t("orgNotFoundTitle") : t("notFoundTitle")}
      description={
        organisation
          ? address
            ? t.rich("orgNotFoundText", {
                address: () => <bdi dir="ltr">{address}</bdi>,
              })
            : t("orgNotFoundLink")
          : signedOut
            ? t("notFoundSignedOut")
            : t("notFoundText")
      }
      action={
        <Button
          variant={signedOut ? "primary" : "secondary"}
          render={<Link href={href} />}
          nativeButton={false}
        >
          {signedOut ? t("signIn") : t("goHome")}
        </Button>
      }
    />
  );
}
