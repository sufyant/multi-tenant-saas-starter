"use client";

import {
  ErrorCode,
  getGetMeQueryOptions,
  getGetUserQueryOptions,
  problemOf,
  schemas,
  type UserDetailResponse,
} from "@repo/api-client";
import { Button } from "@repo/ui/components/button";
import { Skeleton } from "@repo/ui/components/skeleton";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useId, useState } from "react";
import { BackLink } from "@/shared/back-link";
import { DetailList } from "@/shared/detail-list";
import { nameOf, userNameOf } from "@/shared/name-of";
import { NotFoundPage } from "@/shared/not-found-page";
import { ProblemScreen } from "@/shared/problem-screen";
import { systemAdminsPage } from "@/shared/system-admins";
import { useDates } from "@/shared/use-dates";
import { RemoveDialog } from "./remove-dialog";
import { RootLabel } from "./system-admins-page";

/**
 * One system admin (10.15), the page a phone card opens: Back to System admins, the name and the email, when and by whom they were
 * added, and Remove system admin. The "by" part is left out when the API does not know it (a root's first entry, or an event before
 * 37b-backend); a deleted admin is "Deleted user". A root has no Remove and says why; the viewer's own Remove is disabled with its
 * reason (F51). A user who is no system admin has no such page.
 */
export function SystemAdminPage({ userId }: { userId: string }) {
  const known = schemas.GetUserParams.shape.userId.safeParse(userId).success;
  const user = useQuery(
    getGetUserQueryOptions(userId, { query: { enabled: known } }),
  );
  if (
    !known ||
    problemOf(user.error)?.code === ErrorCode.UserNotFound ||
    user.data?.isSystemAdmin === false
  )
    return <NotFoundPage />;
  if (user.error) return <ProblemScreen error={user.error} />;
  if (!user.data) return <Loading />;
  return <Detail user={user.data} />;
}

function Loading() {
  const t = useTranslations("common");
  return <Skeleton rows={3} label={t("loading")} />;
}

function Detail({ user }: { user: UserDetailResponse }) {
  const t = useTranslations();
  const { date } = useDates();
  const me = useQuery(getGetMeQueryOptions());
  const [removing, setRemoving] = useState(false);
  const reasonId = useId();
  const name = userNameOf(user, t("users.deletedUser"));
  const self = user.id === me.data?.id;
  const by =
    user.systemAdminGrantedBy &&
    ((!user.systemAdminGrantedBy.deleted &&
      nameOf(user.systemAdminGrantedBy)) ||
      t("users.deletedUser"));
  const added =
    user.systemAdminGrantedAt &&
    (by
      ? t("systemAdmins.addedBy", {
          date: date(user.systemAdminGrantedAt),
          name: by,
        })
      : date(user.systemAdminGrantedAt));

  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4">
      <BackLink
        href={systemAdminsPage}
        label={t("systemAdmins.backToSystemAdmins")}
      />
      <div className="flex min-w-0 flex-col gap-0.5">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-page-title">
            <bdi>{name}</bdi>
          </h1>
          {user.isRootSystemAdmin && <RootLabel />}
        </div>
        {user.email && (
          <bdi dir="ltr" className="text-small text-muted-foreground">
            {user.email}
          </bdi>
        )}
      </div>
      {added && (
        <DetailList rows={[{ term: t("systemAdmins.added"), value: added }]} />
      )}
      {user.isRootSystemAdmin ? (
        <p className="text-small text-muted-foreground">
          {t("systemAdmins.rootHint")}
        </p>
      ) : (
        <div className="flex flex-col gap-1.5">
          <Button
            variant="destructive"
            className="max-tablet:h-control-lg max-tablet:w-full tablet:self-start"
            // Until GetMe answers, the page does not know whether this is the viewer's own page.
            disabled={self || me.isPending}
            aria-describedby={self ? reasonId : undefined}
            onClick={() => setRemoving(true)}
          >
            {t("systemAdmins.remove")}
          </Button>
          {self && (
            <p id={reasonId} className="text-small text-muted-foreground">
              {t("systemAdmins.cannotRemoveSelf")}
            </p>
          )}
        </div>
      )}
      {removing && (
        <RemoveDialog user={user} onClose={() => setRemoving(false)} />
      )}
    </section>
  );
}
