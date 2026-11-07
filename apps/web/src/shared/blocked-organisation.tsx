"use client";

import { useClerk } from "@clerk/nextjs";
import type {
  MyTenantResponse,
  MyTenantResponseStatus,
} from "@repo/api-client";
import { NoticeScreen } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import Link from "next/link";
import { useTranslations } from "next-intl";

export type BlockedStatus =
  | typeof MyTenantResponseStatus.Suspended
  | typeof MyTenantResponseStatus.Closed;

/**
 * The page of an organisation its members cannot open (frames 2.13 and 2.14), in place of the shell: what happened to the
 * organisation and the ways out. It never shows a reason: the API sends none to a member.
 */
export function BlockedOrganisation({
  tenant,
  status,
  hasOthers,
}: {
  tenant: MyTenantResponse;
  status: BlockedStatus;
  /** The user has another organisation, so "Go to my organisations" shows. */
  hasOthers: boolean;
}) {
  const t = useTranslations();
  const { signOut } = useClerk();
  return (
    <NoticeScreen
      title={t(`blocked.${status}Title`, { organisation: tenant.name })}
      text={t(`blocked.${status}Text`)}
      actions={
        <>
          {hasOthers && (
            <Button
              variant="primary"
              render={<Link href="/" />}
              nativeButton={false}
            >
              {t("blocked.chooseAnother")}
            </Button>
          )}
          <Button onClick={() => void signOut({ redirectUrl: "/sign-in" })}>
            {t("common.signOut")}
          </Button>
        </>
      }
    />
  );
}
