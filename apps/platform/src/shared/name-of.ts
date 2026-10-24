/**
 * A person's names as one, or null when they have none: a deleted user's names are erased (A8), and the screen shows "Deleted
 * user" in its own language (K3).
 */
export const nameOf = (person: {
  firstName: string | null;
  lastName: string | null;
}) => [person.firstName, person.lastName].filter(Boolean).join(" ") || null;
