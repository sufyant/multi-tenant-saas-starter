"use client";

import { useClerk } from "@clerk/nextjs";
import { ErrorCode } from "@repo/api-client";
import { brand } from "@repo/brand";
import { useAppFrame } from "@repo/ui/components/app-frame";
import { AuthHeading, AuthScreen } from "@repo/ui/components/auth-layout";
import { Button } from "@repo/ui/components/button";
import { supportLink } from "@repo/ui/lib/support-link";
import { useTranslations } from "next-intl";

/** The code of the API's refusal, which names the page's texts: `account.user.suspended.title`. */
export type AccountBlock =
  | typeof ErrorCode.UserSuspended
  | typeof ErrorCode.UserDeleted;

/**
 * The page of a user the API refuses on every request (frames 1.40 to 1.47): 403 `user.suspended` or `user.deleted` (A9). A
 * suspended account can ask support, whose email names the version, and sign out; a deleted one has one way out, another email,
 * which signs out.
 */
export function AccountBlocked({ block }: { block: AccountBlock }) {
  const t = useTranslations();
  const { signOut } = useClerk();
  const { version } = useAppFrame();
  const leave = () => void signOut({ redirectUrl: "/sign-in" });
  return (
    <AuthScreen brand={t("meta.title")}>
      <AuthHeading
        title={t(`account.${block}.title`)}
        text={t(`account.${block}.text`)}
      />
      {block === ErrorCode.UserSuspended ? (
        <>
          <Button
            variant="primary"
            className="w-full"
            render={
              <a
                href={supportLink(
                  brand.supportUrl,
                  version && t("appShell.supportBody", { version }),
                )}
              />
            }
            nativeButton={false}
          >
            {t("problems.contactSupport")}
          </Button>
          <Button variant="ghost" className="self-center" onClick={leave}>
            {t("common.signOut")}
          </Button>
        </>
      ) : (
        <Button variant="primary" className="w-full" onClick={leave}>
          {t("account.user.deleted.action")}
        </Button>
      )}
    </AuthScreen>
  );
}
