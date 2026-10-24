import type { MemberResponse, UserNameResponse } from "@repo/api-client";

type Named = Pick<MemberResponse, "firstName" | "lastName" | "deleted"> & {
  email?: string | null;
};

/** A person's name as the screen shows it: the names, else the email; null for a deleted user, who shows "Deleted user" (K3). */
export function nameOf(person: Named | UserNameResponse): string | null {
  if (person.deleted) return null;
  const name = [person.firstName, person.lastName].filter(Boolean).join(" ");
  return name || ("email" in person ? (person.email ?? null) : null);
}
