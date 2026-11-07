"use client";

import { createContext, type ReactNode, useContext } from "react";

const Context = createContext<string | null>(null);

/** `WEB_APP_URL`, which the root layout reads on the server (F7): the web app's origin, the host of every organisation's address. */
export function WebAppUrlProvider({
  value,
  children,
}: {
  value: string;
  children: ReactNode;
}) {
  return <Context value={value}>{children}</Context>;
}

/**
 * An organisation's address as a person types it: the web app's host and the slug, without the scheme ("app.starter.example/
 * northwind"). General and "Organisation not found" write it from this one source.
 */
export function useOrganisationAddress(slug: string): string {
  const url = useContext(Context);
  if (url === null)
    throw new Error("useOrganisationAddress needs WebAppUrlProvider.");
  return `${new URL(url).host}/${slug}`;
}
