"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  type CreateTermsVersionRequest,
  createTermsVersion,
  ErrorCode,
  getGetTermsVersionQueryOptions,
  problemOf,
  savedUnderKey,
  schemas,
  type TermsVersionResponse,
  TermsVersionResponseState,
  updateTermsVersion,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { Skeleton } from "@repo/ui/components/skeleton";
import { showToast } from "@repo/ui/components/toast";
import { Checkbox } from "@repo/ui/components/ui/checkbox";
import { Input as InputBase } from "@repo/ui/components/ui/input";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useFreshRouter } from "@repo/ui/lib/use-fresh-router";
import { useIntentKey } from "@repo/ui/lib/use-intent-key";
import { cn } from "@repo/ui/lib/utils";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { type ReactNode, useEffect, useId } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { BackLink } from "@/shared/back-link";
import { NotFoundPage } from "@/shared/not-found-page";
import { ProblemScreen } from "@/shared/problem-screen";
import { afterAccountRefusal } from "@/shared/refusal";
import {
  refreshTerms,
  termsPage,
  termsVersionPage,
  todayUtc,
  useDay,
} from "@/shared/terms";
import { useDates } from "@/shared/use-dates";

/** Whether a link is an absolute https URL, as the API checks it (`terms_version.url_invalid`). */
const isHttpsUrl = (value: string) => {
  try {
    return new URL(value).protocol === "https:";
  } catch {
    return false;
  }
};

// The generated schema of CreateTermsVersion, which UpdateTermsVersion shares (F11): the form checks before it sends what the API
// checks, a day of today or later in UTC and an https link, with the frame's texts (11.03). The API trims the link and the summary.
const editing = schemas.CreateTermsVersionBody.extend({
  effectiveDate: z.string().refine((day) => day >= todayUtc(), "date"),
  url: z
    .string()
    .trim()
    .max(schemas.createTermsVersionBodyUrlMax)
    .refine(isHttpsUrl, "link"),
  summary: z.string().trim().max(schemas.createTermsVersionBodySummaryMax),
  sendsEmail: z.boolean(),
});
type Editing = z.infer<typeof editing>;

const bodyOf = (form: Editing): Required<CreateTermsVersionRequest> => ({
  effectiveDate: form.effectiveDate,
  url: form.url,
  summary: form.summary || null,
  requiresAcceptance: form.requiresAcceptance,
  sendsEmail: form.sendsEmail,
});

/** New version (11.02 to 11.04): the form starts empty, with both switches on. */
export function NewVersionPage() {
  return <Form version={null} />;
}

/**
 * The scheduled version's edit (11.10). A version that is no longer scheduled never changes, so its edit says why and links back.
 */
export function EditVersionPage({ versionId }: { versionId: string }) {
  const known =
    schemas.GetTermsVersionParams.shape.versionId.safeParse(versionId).success;
  const detail = useQuery(
    getGetTermsVersionQueryOptions(versionId, { query: { enabled: known } }),
  );
  const t = useTranslations();
  if (
    !known ||
    problemOf(detail.error)?.code === ErrorCode.TermsVersionNotFound
  )
    return <NotFoundPage />;
  if (detail.error) return <ProblemScreen error={detail.error} />;
  if (!detail.data) return <Skeleton rows={4} label={t("common.loading")} />;
  const { version } = detail.data;
  if (version.state !== TermsVersionResponseState.Scheduled)
    return (
      <section className="flex min-w-0 flex-1 flex-col gap-4">
        <BackLink
          href={termsVersionPage(version.id)}
          label={t("terms.title")}
        />
        <Banner tone="info" title={t("errors.terms_version.not_scheduled")} />
      </section>
    );
  return <Form key={version.id} version={version} />;
}

/**
 * One row of the form's card (11.02): the label and its help in a 260 px column, the control after it; one above the other on a
 * phone, as the plan editor's rows.
 */
function Row({
  label,
  hint,
  children,
}: {
  label: ReactNode;
  hint?: string;
  children: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-3 px-5 py-3 tablet:flex-row tablet:items-center tablet:gap-6">
      <div className="flex min-w-0 flex-col gap-0.5 tablet:w-65 tablet:shrink-0">
        <span className="text-body font-medium">{label}</span>
        {hint && <p className="text-small text-muted-foreground">{hint}</p>}
      </div>
      <div className="flex min-w-0 flex-1 flex-col gap-1.5">{children}</div>
    </div>
  );
}

/** A switch of the form (11.02): a checkbox with its help beside it, and the reason it is locked under it (F52). */
function Choice({
  id,
  hint,
  checked,
  locked,
  onChange,
}: {
  id: string;
  hint: string;
  checked: boolean;
  locked: string | null;
  onChange: (checked: boolean) => void;
}) {
  return (
    <>
      <div className="flex items-start gap-2">
        <Checkbox
          id={id}
          className="mt-0.5"
          aria-describedby={locked ? `${id}-hint ${id}-locked` : `${id}-hint`}
          checked={checked}
          disabled={locked !== null}
          onCheckedChange={onChange}
        />
        <p
          id={`${id}-hint`}
          className="max-w-110 text-small text-muted-foreground"
        >
          {hint}
        </p>
      </div>
      {locked && (
        <p id={`${id}-locked`} className="text-small text-muted-foreground">
          {locked}
        </p>
      )}
    </>
  );
}

function Form({ version }: { version: TermsVersionResponse | null }) {
  const t = useTranslations();
  const day = useDay();
  const { date } = useDates();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const router = useFreshRouter(useRouter());
  const { key, renew } = useIntentKey();
  const id = useId();
  const form = useForm<Editing>({
    resolver: zodResolver(editing),
    defaultValues: {
      effectiveDate: version?.effectiveDate ?? "",
      url: version?.url ?? "",
      summary: version?.summary ?? "",
      requiresAcceptance: version?.requiresAcceptance ?? true,
      sendsEmail: version?.sendsEmail ?? true,
    },
  });
  // Another value of any field is another intent of CreateTermsVersion: it gets a new key (F48).
  useEffect(
    () => form.subscribe({ formState: { values: true }, callback: renew }),
    [form, renew],
  );
  // Once someone accepted the version, its link and whether it requires acceptance stay; once its email went, the switch stays on
  // (`docs/roadmap.md`, step 38; F52).
  const locked = version?.locked
    ? t("terms.locked", { count: version.acceptedCount })
    : null;
  const emailLocked = version?.emailSentAt
    ? t("terms.emailWentOut", { date: date(version.emailSentAt) })
    : null;

  const refused = (error: unknown) => {
    const code = afterAccountRefusal(queryClient, error);
    if (savedUnderKey(error)) renew();
    if (code === ErrorCode.TermsVersionEffectiveDatePast)
      form.setError("effectiveDate", { message: "date" });
    if (code === ErrorCode.TermsVersionUrlInvalid)
      form.setError("url", { message: "link" });
    // The version changed meanwhile: the screens read again (F47).
    if (
      code === ErrorCode.TermsVersionScheduledExists ||
      code === ErrorCode.TermsVersionLocked ||
      code === ErrorCode.TermsVersionEmailSent ||
      code === ErrorCode.TermsVersionNotScheduled ||
      code === ErrorCode.TermsVersionNotFound
    )
      void refreshTerms(queryClient, version?.id);
  };

  const create = useMutation({
    mutationFn: ({ body, key }: { body: Editing; key: string }) =>
      createTermsVersion(bodyOf(body), { "Idempotency-Key": key }),
    onSuccess: (saved) => {
      renew();
      void refreshTerms(queryClient);
      showToast({
        title: t("terms.added", { date: day(saved.effectiveDate) }),
        description: saved.requiresAcceptance
          ? t("terms.addedText")
          : undefined,
      });
      // The list opens with the new version (11.04).
      void router.push(termsPage);
    },
    onError: refused,
  });
  const update = useMutation({
    mutationFn: ({ id, body }: { id: string; body: Editing }) =>
      updateTermsVersion(id, bodyOf(body)),
    onSuccess: (saved) => {
      void refreshTerms(queryClient, saved.id);
      showToast({
        title: t("terms.saved", { date: day(saved.effectiveDate) }),
      });
      void router.push(termsVersionPage(saved.id));
    },
    onError: refused,
  });
  const pending = create.isPending || update.isPending;

  const submit = form.handleSubmit((values) => {
    if (pending) return;
    if (version) update.mutate({ id: version.id, body: values });
    else create.mutate({ body: values, key });
  });

  const failure = (() => {
    const error = create.error ?? update.error;
    const problem = problemOf(error);
    const code = problem?.code;
    if (
      !error ||
      code === ErrorCode.TermsVersionEffectiveDatePast ||
      code === ErrorCode.TermsVersionUrlInvalid
    )
      return null;
    const known =
      code === ErrorCode.TermsVersionScheduledExists ||
      code === ErrorCode.TermsVersionLocked ||
      code === ErrorCode.TermsVersionEmailSent ||
      code === ErrorCode.TermsVersionNotScheduled ||
      code === ErrorCode.TermsVersionNotFound;
    return known ? t(`errors.${code}`) : failureText(problem);
  })();

  const errors = form.formState.errors;
  const requiresAcceptance = form.watch("requiresAcceptance");
  const sendsEmail = form.watch("sendsEmail");
  const field = (name: "effectiveDate" | "url" | "summary") => `${id}-${name}`;
  const errorOf = (name: "effectiveDate" | "url" | "summary") => {
    const message = errors[name]?.message;
    if (!message) return undefined;
    if (name === "effectiveDate") return t("terms.dateInvalid");
    if (name === "url") return t("terms.linkInvalid");
    return t("terms.summaryTooLong", {
      max: schemas.createTermsVersionBodySummaryMax,
    });
  };
  const input = (
    name: "effectiveDate" | "url" | "summary",
    className: string,
    {
      describedBy,
      ...extra
    }: Record<string, unknown> & {
      describedBy?: string;
    } = {},
  ) => {
    const error = errorOf(name);
    return (
      <>
        <InputBase
          id={field(name)}
          autoComplete="off"
          aria-invalid={error ? true : undefined}
          aria-describedby={
            [error && `${field(name)}-error`, describedBy]
              .filter(Boolean)
              .join(" ") || undefined
          }
          className={cn(
            "h-control-md w-full pointer-coarse:h-control-lg",
            className,
          )}
          {...extra}
          {...form.register(name)}
        />
        {error && (
          <p
            id={`${field(name)}-error`}
            className="text-small text-destructive-text"
          >
            {error}
          </p>
        )}
      </>
    );
  };

  return (
    <section className="flex min-w-0 flex-1 flex-col gap-4">
      <BackLink href={termsPage} label={t("terms.title")} />
      <div className="flex min-w-0 flex-col gap-0.5">
        <h1 className="text-page-title">
          {version ? t("terms.editTitle") : t("terms.newTitle")}
        </h1>
        <p className="max-w-[720px] text-body text-muted-foreground">
          {version
            ? t("terms.editText", { date: day(version.effectiveDate) })
            : t("terms.newText")}
        </p>
      </div>
      <form
        noValidate
        onSubmit={(event) => void submit(event)}
        className="flex flex-col gap-4"
      >
        <div className="flex flex-col divide-y divide-border rounded-lg border border-border bg-card">
          <Row
            label={
              <label htmlFor={field("effectiveDate")}>
                {t("terms.appliesFrom")}
              </label>
            }
            hint={t("terms.appliesFromHint")}
          >
            {input("effectiveDate", "tablet:w-50", {
              type: "date",
              min: todayUtc(),
            })}
          </Row>
          <Row
            label={<label htmlFor={field("url")}>{t("terms.link")}</label>}
            hint={t("terms.linkHint")}
          >
            {input("url", "tablet:w-105", {
              type: "url",
              dir: "ltr",
              inputMode: "url",
              maxLength: schemas.createTermsVersionBodyUrlMax,
              disabled: locked !== null,
              describedBy: locked ? `${field("url")}-locked` : undefined,
            })}
            {locked && (
              <p
                id={`${field("url")}-locked`}
                className="text-small text-muted-foreground"
              >
                {locked}
              </p>
            )}
          </Row>
          <Row
            label={
              <label htmlFor={field("summary")}>{t("terms.summary")}</label>
            }
            hint={t("terms.summaryHint")}
          >
            {input("summary", "tablet:w-105", {
              placeholder: t("terms.summaryPlaceholder"),
              maxLength: schemas.createTermsVersionBodySummaryMax,
            })}
          </Row>
          <Row
            label={
              <label htmlFor={`${id}-acceptance`}>
                {t("terms.requiresAcceptance")}
              </label>
            }
          >
            <Choice
              id={`${id}-acceptance`}
              hint={t("terms.requiresAcceptanceHint")}
              checked={requiresAcceptance}
              locked={locked}
              onChange={(checked) =>
                form.setValue("requiresAcceptance", checked, {
                  shouldDirty: true,
                })
              }
            />
          </Row>
          <Row
            label={
              <label htmlFor={`${id}-email`}>{t("terms.sendEmail")}</label>
            }
          >
            <Choice
              id={`${id}-email`}
              hint={t("terms.sendEmailHint")}
              checked={sendsEmail}
              locked={emailLocked}
              onChange={(checked) =>
                form.setValue("sendsEmail", checked, { shouldDirty: true })
              }
            />
          </Row>
        </div>
        {failure && (
          <Banner tone="destructive" title={t("terms.notSaved")}>
            {failure}
          </Banner>
        )}
        <div>
          <Button
            type="submit"
            variant="primary"
            className="max-tablet:h-control-lg max-tablet:w-full"
            // An edit saves only a change (11.10), as the plan editor; a new version always has one to add.
            disabled={pending || (version !== null && !form.formState.isDirty)}
            aria-busy={pending || undefined}
          >
            {version ? t("terms.save") : t("terms.add")}
          </Button>
        </div>
      </form>
    </section>
  );
}
