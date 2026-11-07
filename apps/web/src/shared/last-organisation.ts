"use server";

import { schemas } from "@repo/api-client";
import { cookies } from "next/headers";
import { lastOrganisationCookie } from "./last-organisation-cookie";

const oneYear = 60 * 60 * 24 * 365;
// The form of a tenant's slug in the contract; anything else never reaches the cookie, because a server action is a public endpoint.
const slug = schemas.CreateTenantBody.shape.slug;

/** Keeps the slug of the organisation the person opened (F30). The same slug writes nothing, so the page does not render again. */
export async function rememberOrganisation(value: string): Promise<void> {
  if (!slug.safeParse(value).success) return;
  const store = await cookies();
  if (store.get(lastOrganisationCookie)?.value === value) return;
  store.set(lastOrganisationCookie, value, {
    path: "/",
    maxAge: oneYear,
    httpOnly: true,
    secure: true,
    sameSite: "lax",
  });
}

/** Deletes the cookie: its organisation is not one the person can open any more. */
export async function forgetOrganisation(): Promise<void> {
  (await cookies()).delete(lastOrganisationCookie);
}
