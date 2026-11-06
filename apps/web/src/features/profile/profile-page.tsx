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
import { brand } from "@repo/brand";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import {
  PreferencesForm,
  PreferencesFormSkeleton,
} from "@repo/ui/components/preferences-form";
import { signInAgainUrl } from "@repo/ui/components/sign-in";
import { ThemeChoice } from "@repo/ui/components/theme-choice";
import { showToast } from "@repo/ui/components/toast";
import { supportLink } from "@repo/ui/lib/support-link";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
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
 * Settings › Profile (the Profile screens): the person's name, language and time zone, which follow them to every organisation
 * and device, and the theme, saved on this device (F24). The form is shared with the platform; the calls are here.
 */
export function ProfilePage() {
  const t = useTranslations();
  const me = useQuery(getGetMeQueryOptions());
  // A refused account goes to its page or to sign-in (F4, F47); any other failure keeps the page and says so in its card (5.06).
  const action = me.error ? actionOf(me.error) : null;
  if (
    me.error &&
    (action?.kind === "sign-in" ||
      action?.kind === "account-suspended" ||
      action?.kind === "account-deleted")
  )
    return <ProblemScreen error={me.error} />;
  return (
    <section className="flex min-w-0 max-w-[760px] flex-col gap-4">
      <div>
        {/* On a phone the top bar holds the page's title, so the page leaves its own out (5.09). */}
        <h1 className="text-page-title max-tablet:hidden">
          {t("profile.title")}
        </h1>
        <p className="mt-1 text-body text-muted-foreground max-tablet:mt-0">
          {me.data
            ? t.rich("profile.intro", {
                name: () => <bdi>{me.data.name}</bdi>,
              })
            : t("profile.introUnknown")}
        </p>
      </div>
      {me.data ? (
        <Preferences me={me.data} />
      ) : me.error ? (
        <Banner
          tone="destructive"
          title={t("preferencesForm.notLoadedTitle")}
          action={
            <Button
              render={<a href={supportLink(brand.supportUrl, undefined)} />}
              nativeButton={false}
            >
              {t("problems.contactSupport")}
            </Button>
          }
        >
          {t("preferencesForm.notLoadedText")}
        </Banner>
      ) : (
        <PreferencesFormSkeleton
          nameNote={t("profile.nameNote")}
          languages={Object.values(Language)}
        />
      )}
      <ThemeChoice />
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
  const failureText = useFailureText();
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
      savedNote={t("profile.savedNote")}
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
        showToast({
          title: t("preferencesForm.notSaved"),
          description: failureText(problemOf(error)),
          tone: "destructive",
        });
      }}
    />
  );
}
