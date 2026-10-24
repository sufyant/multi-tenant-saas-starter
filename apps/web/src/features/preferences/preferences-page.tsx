"use client";

import {
  actionFor,
  type ChangePreferencesRequest,
  changePreferences,
  getGetMeQueryKey,
  getGetMeQueryOptions,
  Language,
  type MeResponse,
  ProblemDetailsError,
  problemOf,
  type RenameMeRequest,
  renameMe,
  schemas,
} from "@repo/api-client";
import { PreferencesForm } from "@repo/ui/components/preferences-form";
import { Skeleton } from "@repo/ui/components/skeleton";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { z } from "zod";
import { setLocale } from "@/i18n/set-locale";
import { ProblemScreen } from "@/shared/problem-screen";
import { SettingsNav } from "@/shared/settings-nav";

// The generated schemas of RenameMe and ChangePreferences (F11). The API trims each name and needs 1 to 100 characters (A7);
// the generated schema has the limit only, so the form adds the trim and the minimum.
const preferences = schemas.RenameMeBody.extend({
  firstName: z.string().trim().min(1).max(schemas.renameMeBodyFirstNameMax),
  lastName: z.string().trim().min(1).max(schemas.renameMeBodyLastNameMax),
}).extend(schemas.ChangePreferencesBody.shape);

const actionOf = (error: unknown) =>
  error instanceof ProblemDetailsError
    ? actionFor(error.status, error.problem)
    : null;

/**
 * Settings > Preferences (the Preferences board): the person's name, language and time zone, which follow them to every
 * organisation and device. The form is shared with the platform; the calls are here. The theme is not here: it lives in the
 * user menu, saved on the device.
 */
export function PreferencesPage() {
  const t = useTranslations();
  const me = useQuery(getGetMeQueryOptions());
  if (me.error) return <ProblemScreen error={me.error} />;
  return (
    <div className="flex flex-col gap-6 tablet:flex-row">
      <SettingsNav current="preferences" />
      <section className="flex min-w-0 max-w-[640px] flex-1 flex-col gap-4">
        <div>
          <h1 className="text-page-title">{t("preferences.title")}</h1>
          <p className="mt-1 text-body text-muted-foreground">
            {t("preferences.text")}
          </p>
        </div>
        {me.data ? (
          <Preferences me={me.data} />
        ) : (
          <Skeleton label={t("common.loading")} />
        )}
      </section>
    </div>
  );
}

/**
 * The calls of the form. After a save the screen reads GetMe again, so the top bar and the user menu show the new name (F47),
 * and a new language sets the locale cookie and renders the page again (F13).
 */
function Preferences({ me }: { me: MeResponse }) {
  const t = useTranslations();
  const locale = useLocale();
  const router = useRouter();
  const queryClient = useQueryClient();
  // Each command goes through useMutation (F2); the form waits for both answers.
  const rename = useMutation({
    mutationFn: (names: RenameMeRequest) => renameMe(names),
  });
  const change = useMutation({
    mutationFn: (choice: ChangePreferencesRequest) => changePreferences(choice),
  });
  const failureText = useFailureText();
  return (
    <PreferencesForm
      values={{
        firstName: me.firstName ?? "",
        lastName: me.lastName ?? "",
        language: me.language,
        timeZone: me.timeZone,
      }}
      schema={preferences}
      languages={Object.values(Language)}
      nameNote={t("preferences.nameNote")}
      rename={rename.mutateAsync}
      changePreferences={change.mutateAsync}
      fieldCodesOf={(error) => {
        const action = actionOf(error);
        return action?.kind === "field-errors" ? action.fields : null;
      }}
      onSaved={async (saved) => {
        await queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() });
        const language = saved.preferences?.language;
        if (!language || language === locale) return;
        await setLocale(language);
        router.refresh();
      }}
      onFailed={(error) => {
        const action = actionOf(error);
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
          title: t("preferencesForm.notSaved"),
          description: failureText(problemOf(error)),
          tone: "destructive",
        });
      }}
    />
  );
}
