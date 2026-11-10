"use client";

import { useAppFrame } from "../components/app-frame";

/**
 * An organisation's address as a person types it: the host of `WEB_APP_URL`, which the root layout gives the frame (F7), and the
 * slug, without the scheme ("app.starter.example/northwind"). The web's General and "Organisation not found" and the platform's Organisations write it from this one source.
 */
export function useOrganisationAddress(slug: string): string {
  const { webAppUrl } = useAppFrame();
  if (!webAppUrl)
    throw new Error("useOrganisationAddress needs the frame's webAppUrl.");
  return `${new URL(webAppUrl).host}/${slug}`;
}
