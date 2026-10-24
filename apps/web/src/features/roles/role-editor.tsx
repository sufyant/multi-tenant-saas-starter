"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import {
  actionFor,
  createRole,
  ErrorCode,
  getListRolesQueryKey,
  Permission,
  ProblemDetailsError,
  problemOf,
  type RoleDetailResponse,
  schemas,
  updateRole,
} from "@repo/api-client";
import { Banner } from "@repo/ui/components/banner";
import { Button } from "@repo/ui/components/button";
import { Dialog } from "@repo/ui/components/dialog";
import { Input } from "@repo/ui/components/input";
import { showToast } from "@repo/ui/components/toast";
import { useFailureText } from "@repo/ui/lib/use-failure-text";
import { useIntentKey } from "@repo/ui/lib/use-intent-key";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { afterRefusal, refreshRoles, refreshTenant } from "@/shared/refresh";
import { nameTaken } from "@/shared/roles";
import { useCan, useTenant } from "@/shared/tenant-context";
import { holdsAll, useTenantRoles } from "@/shared/use-can-act-on";
import { DeleteRoleDialog } from "./delete-role-dialog";
import { PermissionList } from "./permission-list";
import { withRequired } from "./permissions";
import { useLeaveGuard } from "./use-leave-guard";

type RoleForm = z.infer<typeof schemas.CreateRoleBody>;

const nameMax = schemas.createRoleBodyNameMax;

/**
 * A new role, or a custom role's changes (the RoleCreate, RoleEdit and RoleUnsaved boards), with the generated schema of CreateRole
 * (F11): the name, trimmed, 1 to 100 characters and unique in the organisation, then the permissions in their groups. The screen
 * holds the API's rules before it refuses: See the organisation's details is locked on, See roles is locked on by the four
 * permissions that give or take a role, a permission the viewer lacks is disabled (F51), Manage Owners never shows. Create sends
 * one Idempotency-Key per intent (F48); Save sends the name and every permission the role holds afterwards. Leaving with changes
 * asks first.
 */
export function RoleEditor({ role }: { role?: RoleDetailResponse }) {
  const t = useTranslations();
  const failureText = useFailureText();
  const queryClient = useQueryClient();
  const router = useRouter();
  const can = useCan();
  const { tenant, detail } = useTenant();
  const roles = useTenantRoles();
  const { key, renew } = useIntentKey();
  const [deleting, setDeleting] = useState(false);
  const list = `/${tenant.slug}/settings/roles`;
  const initial = useMemo(
    () => ({
      name: role?.name ?? "",
      permissions: withRequired(role?.permissions ?? []),
    }),
    [role],
  );

  const schema = useMemo(
    () =>
      schemas.CreateRoleBody.extend({
        name: z.string().trim().min(1).max(nameMax),
      }).superRefine((value, context) => {
        if (nameTaken(roles.data?.items ?? [], value.name, role?.id))
          context.addIssue({
            code: "custom",
            path: ["name"],
            message: t("roles.nameTaken", { name: value.name }),
          });
      }),
    [roles.data, role?.id, t],
  );
  const form = useForm<RoleForm>({
    resolver: zodResolver(schema),
    defaultValues: initial,
  });
  const name = form.watch("name");
  const permissions = withRequired(form.watch("permissions"));
  const [done, setDone] = useState(false);
  const dirty =
    !done &&
    (name.trim() !== initial.name ||
      permissions.join() !== initial.permissions.join());
  const guard = useLeaveGuard(dirty);
  // Another name or another permission is another intent: it gets a new key (F48).
  useEffect(
    () => form.subscribe({ formState: { values: true }, callback: renew }),
    [form, renew],
  );

  const save = useMutation({
    mutationFn: (body: RoleForm) =>
      role
        ? updateRole(tenant.id, role.id, body)
        : createRole(tenant.id, body, { "Idempotency-Key": key }),
    onSuccess: () => refreshRoles(queryClient, tenant.id),
    onError: (error) => {
      afterRefusal(queryClient, tenant.id, error);
      if (!(error instanceof ProblemDetailsError)) return;
      // The API saved this answer under the key (I6); a request still running and a server failure keep it (F48).
      if (
        error.status < 500 &&
        error.problem?.code !== ErrorCode.IdempotencyKeyInProgress
      )
        renew();
      const code = error.problem?.code;
      const action = actionFor(error.status, error.problem);
      if (action.kind === "field-errors" && action.fields.name?.[0])
        form.setError("name", {
          message: `validation.${action.fields.name[0]}`,
        });
      if (code === ErrorCode.RoleNameInvalid)
        form.setError("name", { message: "validation.required" });
      if (code === ErrorCode.RoleNameTaken) {
        form.setError("name", {
          message: t("roles.nameTaken", { name: form.getValues("name") }),
        });
        void queryClient.invalidateQueries({
          queryKey: getListRolesQueryKey(tenant.id),
        });
      }
      if (code === ErrorCode.RoleBuiltIn)
        void refreshRoles(queryClient, tenant.id);
      if (code === ErrorCode.RoleNotPermitted) {
        void refreshTenant(queryClient, tenant.id);
        showToast({ title: t("roles.notPermitted"), tone: "destructive" });
      }
      if (code === ErrorCode.RoleNotFound) {
        setDone(true);
        guard.release();
        showToast({
          title: t("roles.gone"),
          description: t("roles.goneText"),
          tone: "info",
        });
        router.push(list);
      }
    },
  });

  const banner = (() => {
    if (!save.error) return null;
    const problem = problemOf(save.error);
    const code = problem?.code;
    if (
      code === ErrorCode.RoleNameTaken ||
      code === ErrorCode.RoleNameInvalid ||
      code === ErrorCode.RoleNotPermitted ||
      code === ErrorCode.RoleNotFound
    )
      return null;
    if (
      code === ErrorCode.RequestValidationFailed &&
      save.error instanceof ProblemDetailsError &&
      Object.keys(problem?.errors ?? {}).every((field) => field === "name")
    )
      return null;
    const own = {
      [ErrorCode.RolePermissionsRequired]: t(
        "errors.role.permissions_required",
      ),
      [ErrorCode.RoleProfileReadRequired]: t("roles.profileReadRequired"),
      [ErrorCode.RoleRolesReadRequired]: t("errors.role.roles_read_required"),
      [ErrorCode.RolePermissionOwnerOnly]: t("roles.ownerOnly"),
      [ErrorCode.RolePermissionInvalid]: t("roles.permissionInvalid"),
      [ErrorCode.RoleEscalation]: t("roles.lacks"),
      [ErrorCode.RoleLimitReached]: t("errors.role.limit_reached"),
    } as Partial<Record<string, string>>;
    if (code === ErrorCode.RoleBuiltIn)
      return { title: t("roles.builtInTitle"), text: t("roles.builtInText") };
    const title = code ? own[code] : undefined;
    if (title) return { title, text: null };
    return {
      title: role ? t("roles.saveFailed") : t("roles.createFailed"),
      text: failureText(problemOf(save.error)),
    };
  })();

  const submit = form.handleSubmit(
    (values) =>
      save.isPending ||
      save.mutate(
        { name: values.name, permissions: withRequired(values.permissions) },
        {
          onSuccess: (saved) => {
            renew();
            setDone(true);
            guard.release();
            showToast(
              role
                ? { title: t("roles.saved") }
                : {
                    title: t("roles.created"),
                    description: t("roles.createdText", { name: saved.name }),
                  },
            );
            router.push(list);
          },
        },
      ),
  );

  const nameError = form.formState.errors.name?.message;
  const nameErrorText = (() => {
    if (!nameError) return undefined;
    if (nameError === "validation.too_long")
      return t("roles.nameLength", { max: nameMax });
    if (nameError.startsWith("validation.")) return t("roles.nameRequired");
    return nameError;
  })();
  const toggle = (permission: Permission, checked: boolean) =>
    form.setValue(
      "permissions",
      checked
        ? [...permissions, permission]
        : permissions.filter((held) => held !== permission),
    );
  const canDelete =
    role !== undefined &&
    can(Permission.TenantRolesDelete) &&
    holdsAll(detail.permissions, role);

  return (
    <>
      <h1 className="text-page-title">
        <bdi>{role ? role.name : t("roles.newRole")}</bdi>
      </h1>
      <form
        noValidate
        onSubmit={(event) => void submit(event)}
        className="flex max-w-3xl flex-col gap-5"
      >
        <Input
          label={t("roles.name")}
          autoComplete="off"
          maxLength={nameMax}
          hint={t("roles.nameHint", { organisation: tenant.name })}
          error={nameErrorText}
          {...form.register("name")}
        />
        <PermissionList
          held={permissions}
          editable={{ viewer: detail.permissions, onChange: toggle }}
        />
        {banner && (
          <Banner tone="destructive" title={banner.title}>
            {banner.text}
          </Banner>
        )}
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <Button
              type="submit"
              variant="primary"
              disabled={save.isPending}
              aria-busy={save.isPending || undefined}
            >
              {role ? t("roles.save") : t("roles.create")}
            </Button>
            <Button render={<Link href={list} />} nativeButton={false}>
              {t("common.cancel")}
            </Button>
          </div>
          {canDelete && (
            <Button variant="destructive" onClick={() => setDeleting(true)}>
              {t("roles.delete")}
            </Button>
          )}
        </div>
      </form>
      {deleting && role && (
        <DeleteRoleDialog
          role={role}
          onClose={() => setDeleting(false)}
          onDeleted={() => {
            setDone(true);
            guard.release();
            router.push(list);
          }}
        />
      )}
      {guard.leaving && (
        <Dialog
          open
          onOpenChange={(open) => !open && guard.keep()}
          title={
            role
              ? t("roles.discardTitle", { name: role.name })
              : t("roles.discardNewTitle")
          }
          description={t("roles.discardText")}
          actionLabel={t("roles.discard")}
          cancelLabel={t("roles.keepEditing")}
          onConfirm={guard.discard}
        />
      )}
    </>
  );
}
