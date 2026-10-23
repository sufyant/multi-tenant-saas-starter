"use client";

import { useClerk } from "@clerk/nextjs";
import {
  type MyTenantResponse,
  MyTenantResponseStatus,
} from "@repo/api-client";
import { AuthLayout } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { Status } from "@repo/ui/components/status";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { DetailList } from "./detail-list";
import { useRoleName } from "./use-role-name";

export type BlockedStatus =
  | typeof MyTenantResponseStatus.Suspended
  | typeof MyTenantResponseStatus.Closed;

/**
 * The page of an organisation its members cannot open (the Suspended and Closed boards), in place of the shell: the status, the
 * organisation, the person's role, and the ways out. It never shows a reason: the API sends none to a member.
 */
export function BlockedOrganisation({
  tenant,
  status,
  hasOthers,
}: {
  tenant: MyTenantResponse;
  status: BlockedStatus;
  /** The user has another organisation, so "Choose another organisation" shows. */
  hasOthers: boolean;
}) {
  const t = useTranslations();
  const roleName = useRoleName();
  const { signOut } = useClerk();
  return (
    <AuthLayout brand={t("meta.title")}>
      <div className="flex flex-col gap-5">
        <Status
          tone={
            status === MyTenantResponseStatus.Suspended ? "warning" : "neutral"
          }
        >
          {t(`organisations.status.${status}`)}
        </Status>
        <h1 className="text-page-title">
          {t(`blocked.${status}Title`, { organisation: tenant.name })}
        </h1>
        <p className="text-body text-muted-foreground">
          {t(`blocked.${status}Text`)}
        </p>
        <DetailList
          rows={[
            {
              term: t("blocked.organisation"),
              value: (
                <bdi dir="ltr" className="font-mono text-mono">
                  {tenant.slug}
                </bdi>
              ),
            },
            {
              term: t("general.yourRole"),
              value: roleName(tenant.role),
            },
          ]}
        />
        <div className="flex flex-wrap gap-2">
          {hasOthers && (
            <Button
              variant="primary"
              render={<Link href="/" />}
              nativeButton={false}
            >
              {t("blocked.chooseAnother")}
            </Button>
          )}
          <Button
            variant="ghost"
            onClick={() => void signOut({ redirectUrl: "/sign-in" })}
          >
            {t("common.signOut")}
          </Button>
        </div>
      </div>
    </AuthLayout>
  );
}
