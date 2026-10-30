"use client";

import {
  actionFor,
  type ChangeEmailPreferencesRequest,
  changeEmailPreferences,
  getGetEmailPreferencesQueryKey,
  getGetEmailPreferencesQueryOptions,
  getGetMeQueryKey,
  NotificationCategory,
  ProblemDetailsError,
  problemOf,
} from "@repo/api-client";
import { NotificationPreferences } from "@repo/ui/components/notification-preferences";
import { Skeleton } from "@repo/ui/components/skeleton";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { ProblemScreen } from "@/shared/problem-screen";
import { SettingsNav } from "@/shared/settings-nav";

/**
 * Settings › You › Notifications (the NotificationPrefs boards): the person's email preferences, the same in every organisation.
 * Both apps share the form; the calls are here. The email's footer names this page.
 */
export function NotificationsPage() {
  const t = useTranslations();
  const preferences = useQuery(getGetEmailPreferencesQueryOptions());
  if (preferences.error) return <ProblemScreen error={preferences.error} />;
  return (
    <div className="flex flex-col gap-6 tablet:flex-row">
      <SettingsNav current="notifications" />
      <section className="flex min-w-0 max-w-[640px] flex-1 flex-col gap-4">
        <div>
          <h1 className="text-page-title">{t("notificationSettings.title")}</h1>
          <p className="mt-1 text-body text-muted-foreground">
            {t("notificationSettings.text")}
          </p>
        </div>
        {preferences.data ? (
          <Form values={preferences.data} />
        ) : (
          <Skeleton label={t("common.loading")} />
        )}
      </section>
    </div>
  );
}

/** The call of the form: ChangeEmailPreferences, then GetEmailPreferences again (F47). */
function Form({ values }: { values: ChangeEmailPreferencesRequest }) {
  const t = useTranslations();
  const queryClient = useQueryClient();
  const failureText = useFailureText();
  const change = useMutation({
    mutationFn: (choice: ChangeEmailPreferencesRequest) =>
      changeEmailPreferences(choice),
  });
  return (
    <NotificationPreferences
      values={values}
      removedNote={values.categories.some(
        ({ category }) => category === NotificationCategory.Access,
      )}
      save={change.mutateAsync}
      onSaved={() =>
        queryClient.invalidateQueries({
          queryKey: getGetEmailPreferencesQueryKey(),
        })
      }
      onFailed={(error) => {
        const action =
          error instanceof ProblemDetailsError
            ? actionFor(error.status, error.problem)
            : null;
        // A 401 goes to sign-in; a suspended or deleted user reads GetMe again, and the shell shows its screen (F4, F47).
        if (action?.kind === "sign-in")
          return window.location.assign("/sign-in");
        if (
          action?.kind === "account-suspended" ||
          action?.kind === "account-deleted"
        )
          return void queryClient.invalidateQueries({
            queryKey: getGetMeQueryKey(),
          });
        showToast({
          title: t("notificationPreferences.notSaved"),
          description: failureText(problemOf(error)),
          tone: "destructive",
        });
      }}
    />
  );
}
