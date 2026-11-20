"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  actionFor,
  createMyTenant,
  ErrorCode,
  FieldErrorCode,
  getGetMeQueryKey,
  getGetMeQueryOptions,
  getGetMyTenantQueryOptions,
  getGetPlanChoicesQueryOptions,
  getGetSignUpQueryOptions,
  getListMyTenantsQueryKey,
  getListRolesQueryOptions,
  inviteMember,
  type MyTenantResponse,
  ProblemDetailsError,
  problemOf,
  ReservedSlug,
  type SignUpTrialResponse,
  savedUnderKey,
  schemas,
} from "@repo/api-client";
import { AuthHeading, AuthScreen } from "@repo/ui/components/auth-layout";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { Input } from "@repo/ui/components/input";
import { Skeleton } from "@repo/ui/components/skeleton";
import { formatDate } from "@repo/ui/lib/format-date-time";
import { formatMoney } from "@repo/ui/lib/format-money";
import { useOrganisationAddress } from "@repo/ui/lib/organisation-address";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useFreshRouter } from "@repo/ui/lib/use-fresh-router";
import { useIntentKey } from "@repo/ui/lib/use-intent-key";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { type FormEvent, useEffect, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { firstInviteRole } from "@/shared/roles";

// The generated schema of CreateMyTenant (F11), with the trim and the minimum of the name, and the reserved slugs refused before the
// request with the API's field code; the API still decides (F58).
const reservedSlugs: readonly string[] = Object.values(ReservedSlug);
const creation = schemas.CreateMyTenantBody.extend({
  name: z.string().trim().min(1).max(schemas.createMyTenantBodyNameMax),
  slug: schemas.CreateMyTenantBody.shape.slug.refine(
    (slug) => !reservedSlugs.includes(slug),
    FieldErrorCode.SlugReserved,
  ),
});
type Creation = z.infer<typeof creation>;

/** The address a name gives: lower-case letters and digits, without their marks, with one hyphen between the words. */
export const slugOf = (name: string) =>
  name
    .normalize("NFKD")
    .replace(/\p{M}/gu, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .slice(0, schemas.createMyTenantBodySlugMax)
    .replace(/^-+|-+$/g, "");

/**
 * Create your organisation (1.28, 1.29, 1.33), while the product has sign-up on: the name, and the address it gives until the person
 * types one. The person becomes the Owner; while billing is on the organisation starts its trial (GetSignUp's `trial`), and the trial's
 * start follows (1.30, 1.34). While billing is off Home opens at once. With sign-up off the page leads to "Choose an organisation".
 */
export function CreateOrganisation() {
  const router = useFreshRouter(useRouter());
  const t = useTranslations();
  const signUp = useQuery(getGetSignUpQueryOptions());
  const [created, setCreated] = useState<MyTenantResponse>();
  const off = signUp.data?.enabled === false;
  useEffect(() => {
    if (off) router.replace("/");
  }, [off, router]);
  if (!signUp.data?.enabled) return <Skeleton label={t("common.loading")} />;
  const trial = signUp.data.trial;
  if (created && trial)
    return <TrialStarted organisation={created} trial={trial} />;
  return (
    <CreationForm
      trial={trial}
      limit={signUp.data.tenantLimit}
      onCreated={(organisation) =>
        trial ? setCreated(organisation) : router.push(`/${organisation.slug}`)
      }
    />
  );
}

function CreationForm({
  trial,
  limit,
  onCreated,
}: {
  trial: SignUpTrialResponse | null;
  limit: number;
  onCreated: (organisation: MyTenantResponse) => void;
}) {
  const t = useTranslations("createOrganisation");
  const all = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const { key, renew } = useIntentKey();
  const [slugTyped, setSlugTyped] = useState(false);
  const form = useForm<Creation>({
    resolver: zodResolver(creation),
    defaultValues: { name: "", slug: "" },
  });
  // Another name or address is another intent: it gets a new key (F48).
  useEffect(
    () => form.subscribe({ formState: { values: true }, callback: renew }),
    [form, renew],
  );

  const create = useMutation({
    mutationFn: (body: Creation) =>
      createMyTenant(body, { "Idempotency-Key": key }),
    onSuccess: async (organisation) => {
      renew();
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: getListMyTenantsQueryKey() }),
        queryClient.invalidateQueries({ queryKey: getGetMeQueryKey() }),
      ]);
      onCreated(organisation);
    },
    onError: (error) => {
      // A refusal the API saved under the key replays for that key: the next request takes a new one (F48).
      if (savedUnderKey(error)) renew();
      if (!(error instanceof ProblemDetailsError)) return;
      const code = error.problem?.code;
      if (code === ErrorCode.TenantNameInvalid)
        form.setError("name", { message: code });
      if (code === ErrorCode.TenantSlugTaken)
        form.setError("slug", { message: FieldErrorCode.SlugReserved });
      const action = actionFor(error.status, error.problem);
      if (action.kind !== "field-errors") return;
      for (const field of ["name", "slug"] as const) {
        const [fieldCode] = action.fields[field] ?? [];
        if (fieldCode) form.setError(field, { message: fieldCode });
      }
    },
  });

  const code = problemOf(create.error)?.code;
  const failure =
    !create.error ||
    code === ErrorCode.RequestValidationFailed ||
    code === ErrorCode.TenantNameInvalid ||
    code === ErrorCode.TenantSlugTaken
      ? null
      : code === ErrorCode.TenantCreationLimitReached
        ? all("shell.creationLimit", { limit, count: limit })
        : failureText(problemOf(create.error));

  const submit = form.handleSubmit(
    (body) => create.isPending || create.mutate(body),
  );
  const errors = form.formState.errors;
  const name = form.register("name", {
    onChange: (event: { target: { value: string } }) => {
      if (!slugTyped) form.setValue("slug", slugOf(event.target.value));
    },
  });
  const slug = form.register("slug", { onChange: () => setSlugTyped(true) });
  const address = useOrganisationAddress(form.watch("slug"));
  const nameError = errors.name?.message;
  const slugError = errors.slug?.message;

  return (
    <AuthScreen brand={all("meta.title")}>
      <AuthHeading
        align="start"
        title={t("title")}
        text={
          trial
            ? t("textTrial", { plan: trial.plan.name, days: trial.days })
            : t("text")
        }
      />
      {failure && (
        <Banner tone="destructive" title={t("notCreated")}>
          {failure}
        </Banner>
      )}
      <form
        noValidate
        onSubmit={(event) => void submit(event)}
        className="flex flex-col gap-4"
      >
        <Input
          label={t("name")}
          autoComplete="organization"
          error={
            nameError &&
            (nameError === FieldErrorCode.TooLong ||
            nameError === "validation.too_long"
              ? t("nameTooLong", { max: schemas.createMyTenantBodyNameMax })
              : t("nameRequired"))
          }
          {...name}
        />
        <Input
          label={t("address")}
          dir="ltr"
          inputMode="url"
          autoComplete="off"
          maxLength={schemas.createMyTenantBodySlugMax}
          hint={<bdi dir="ltr">{address}</bdi>}
          error={
            slugError &&
            (slugError === FieldErrorCode.SlugReserved
              ? t("addressTaken")
              : t("addressInvalid"))
          }
          {...slug}
        />
        <Button
          type="submit"
          variant="primary"
          className="w-full"
          disabled={create.isPending}
        >
          {t("create")}
        </Button>
      </form>
      <p className="text-small text-muted-foreground">
        {all("createOrganisation.limit", { limit })}
      </p>
    </AuthScreen>
  );
}

/**
 * The trial has started (1.30, 1.34): its plan, its length and its end, and "Invite your team": addresses separated by commas, each
 * invited as Member in the person's language, one after the other. The hint says what a seat costs after the trial, from the trial
 * plan's monthly prices, and nothing of a plan that costs nothing. Each address passes InviteMember's generated schema (F11) before
 * any is sent. Send invitations and Skip for now open the organisation's Home.
 */
function TrialStarted({
  organisation,
  trial,
}: {
  organisation: MyTenantResponse;
  trial: SignUpTrialResponse;
}) {
  const t = useTranslations("createOrganisation");
  const all = useTranslations();
  const router = useFreshRouter(useRouter());
  const failureText = useFailureText();
  const me = useQuery(getGetMeQueryOptions());
  const detail = useQuery(getGetMyTenantQueryOptions(organisation.id));
  const choices = useQuery(getGetPlanChoicesQueryOptions(organisation.id));
  const roles = useQuery(getListRolesQueryOptions(organisation.id));
  const member = firstInviteRole(roles.data?.items ?? []);
  const [emails, setEmails] = useState("");
  const [invalid, setInvalid] = useState<string>();
  const [failed, setFailed] = useState<{ email: string; error: unknown }>();
  // One Idempotency-Key per address (F48): a retry after a network error sends the same key, so nobody is invited twice; an answer
  // the API saved, a success or a refusal, frees it.
  const keys = useRef(new Map<string, string>());
  const keyOf = (email: string) => {
    const known = keys.current.get(email);
    if (known) return known;
    const key = crypto.randomUUID();
    keys.current.set(email, key);
    return key;
  };
  const home = `/${organisation.slug}`;
  const locale = all("locale.format");

  const send = useMutation({
    mutationFn: async (addresses: string[]) => {
      if (!member) return;
      const language = me.data?.language;
      for (const [index, email] of addresses.entries()) {
        try {
          await inviteMember(
            organisation.id,
            { email, roleId: member.id, language },
            { "Idempotency-Key": keyOf(email) },
          );
          keys.current.delete(email);
        } catch (error) {
          if (savedUnderKey(error)) keys.current.delete(email);
          // The addresses already invited leave the field; the one that failed and the rest stay, so a retry sends only them.
          setEmails(addresses.slice(index).join(", "));
          setFailed({ email, error });
          return;
        }
      }
      router.push(home);
    },
  });

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const addresses = emails
      .split(",")
      .map((email) => email.trim())
      .filter((email) => email !== "");
    const wrong = addresses.find(
      (email) => !schemas.InviteMemberBody.shape.email.safeParse(email).success,
    );
    setInvalid(wrong);
    setFailed(undefined);
    if (wrong) return;
    if (addresses.length === 0) return router.push(home);
    send.mutate(addresses);
  };

  const endsAt = detail.data?.trial?.endsAt;
  const date =
    endsAt &&
    formatDate(endsAt, { locale, timeZone: me.data?.timeZone ?? undefined });
  const plan = choices.data?.plans.find((item) => item.id === trial.plan.id);
  const money = (minor: number) =>
    formatMoney(minor, plan?.currency ?? "EUR", locale);
  const base = plan?.prices.baseMonthly ?? 0;
  const perPerson = plan?.prices.perPersonMonthly ?? 0;
  const price =
    base > 0 && perPerson > 0
      ? t("priceBoth", { base: money(base), perPerson: money(perPerson) })
      : perPerson > 0
        ? t("pricePerPerson", { perPerson: money(perPerson) })
        : base > 0
          ? t("priceBase", { base: money(base) })
          : null;
  const domain = me.data?.email?.split("@")[1] ?? "example.com";

  return (
    <AuthScreen brand={all("meta.title")}>
      <AuthHeading
        align="start"
        title={t("startedTitle", { plan: trial.plan.name, days: trial.days })}
        text={
          date &&
          t("startedText", {
            organisation: organisation.name,
            plan: trial.plan.name,
            date,
          })
        }
      />
      {failed && (
        <Banner
          tone="destructive"
          title={t("notInvited", { email: failed.email })}
        >
          {failureText(problemOf(failed.error))}
        </Banner>
      )}
      <form noValidate onSubmit={submit} className="flex flex-col gap-4">
        <Input
          label={t("inviteTeam")}
          type="text"
          inputMode="email"
          autoComplete="off"
          placeholder={t("invitePlaceholder", { domain })}
          value={emails}
          onChange={(event) => setEmails(event.target.value)}
          hint={[t("inviteHint"), price].filter(Boolean).join(" ")}
          error={invalid && t("notAnEmail", { email: invalid })}
          disabled={send.isPending}
        />
        <Button
          type="submit"
          variant="primary"
          className="w-full"
          disabled={send.isPending || !member}
        >
          {t("sendInvitations")}
        </Button>
      </form>
      <Button
        variant="ghost"
        className="self-center"
        onClick={() => router.push(home)}
        disabled={send.isPending}
      >
        {t("skip")}
      </Button>
    </AuthScreen>
  );
}
