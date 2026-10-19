import {
  type InviterNameResponse,
  InviterNameResponseKind,
} from "@repo/api-client";

/** How the inviter is named (the AcceptEnded board): a person by name, else the platform, else "the person who invited you". */
export type Inviter =
  | { kind: "person"; name: string }
  | { kind: "platform" }
  | { kind: "deletedUser" };

export function inviterOf(name: InviterNameResponse): Inviter {
  if (name.kind === InviterNameResponseKind.Platform)
    return { kind: "platform" };
  const full = [name.firstName, name.lastName].filter(Boolean).join(" ");
  if (name.deleted || full === "") return { kind: "deletedUser" };
  return { kind: "person", name: full };
}
