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
  type RenameMeRequest,
  renameMe,
  schemas,
} from "@repo/api-client";
import { PreferencesForm } from "@repo/ui/components/preferences-form";
import { signInAgainUrl } from "@repo/ui/components/sign-in";
import { Skeleton } from "@repo/ui/components/skeleton";
import { ThemeChoice } from "@repo/ui/components/theme-choice";
import { showToast } from "@repo/ui/components/toast";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { z } from "zod";
import { setLocale } from "@/i18n/set-locale";
import { ProblemScreen } from "@/shared/problem-screen";

// The generated schemas of RenameMe and ChangePreferences (F11). The API trims the name and needs 1 to 100 characters (A7);
// the generated schema has the limit only, so the form adds the trim and the minimum.
const preferences = schemas.RenameMeBody.extend({
  name: z.string().trim().min(1).max(schemas.renameMeBodyNameMax),
}).extend(schemas.ChangePreferencesBody.shape);

const actionOf = (error: unknown) =>
  error instanceof ProblemDetailsError
    ? actionFor(error.status, error.problem)
    : null;

/**
 * Settings › Profile (F31, the platform's Profile screens): the form of the tenant web for the signed-in user's name, language and
 * time zone, then the theme, which is saved on the device (F24). The email is on Account and security (F62).
 */
export function ProfilePage() {
  const t = useTranslations();
  const me = useQuery(getGetMeQueryOptions());

  if (me.error) return <ProblemScreen error={me.error} />;

  return (
    <section className="flex min-w-0 max-w-[640px] flex-col gap-4">
      <div>
        <h1 className="text-page-title">{t("profile.title")}</h1>
        <p className="mt-1 text-body text-muted-foreground">
          {t("profile.text")}
        </p>
      </div>
      {me.data ? (
        <>
          <Preferences me={me.data} />
          <ThemeChoice />
        </>
      ) : (
        <Skeleton label={t("common.loading")} />
      )}
    </section>
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
  return (
    <PreferencesForm
      values={{
        name: me.name ?? "",
        language: me.language,
        timeZone: me.timeZone,
      }}
      schema={preferences}
      nameMax={schemas.renameMeBodyNameMax}
      languages={Object.values(Language)}
      nameNote={t("profile.nameNote")}
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
          return window.location.assign(signInAgainUrl(window.location));
        if (
          action?.kind === "account-suspended" ||
          action?.kind === "account-deleted"
        )
          return void queryClient.invalidateQueries({
            queryKey: getGetMeQueryKey(),
          });
        const key =
          action?.kind === "message" ? action.key : "errors.server.error";
        showToast({
          title: t("preferencesForm.notSaved"),
          description: t.has(key) ? t(key) : t("errors.server.error"),
          tone: "destructive",
        });
      }}
    />
  );
}
