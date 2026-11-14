"use client";

import {
  SsoCallback,
  type SsoCallbackProps,
} from "@repo/ui/components/sign-in";
import { useTermsUrl } from "@/shared/terms-link";

/** The SsoCallback of packages/ui with the footer's "Terms" of the version in force, which the server route cannot read. */
export function SsoCallbackScreen(props: Omit<SsoCallbackProps, "termsUrl">) {
  return <SsoCallback {...props} termsUrl={useTermsUrl()} />;
}
