import type { MemberResponse, UserNameResponse } from "@repo/api-client";

type Named = Pick<MemberResponse, "name" | "deleted"> & {
  email?: string | null;
};

/** A person's name as the screen shows it: the whole name, else the email; null for a deleted user, who shows "Deleted user" (K3). */
export function nameOf(person: Named | UserNameResponse): string | null {
  if (person.deleted) return null;
  return person.name || ("email" in person ? (person.email ?? null) : null);
}
