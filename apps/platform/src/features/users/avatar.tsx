import { cn } from "@repo/ui/lib/utils";
import type { User } from "./user";

/** A person's round avatar with their initials (the boards); a deleted user has none. An organisation's is square. */
export function Avatar({
  user,
  className,
}: {
  user: Pick<User, "firstName" | "lastName">;
  className?: string;
}) {
  const initials = [user.firstName, user.lastName]
    .map((name) => name?.charAt(0) ?? "")
    .join("");
  return (
    <span
      aria-hidden="true"
      className={cn(
        "flex size-8 shrink-0 items-center justify-center rounded-full bg-muted text-small font-semibold",
        className,
      )}
    >
      {initials}
    </span>
  );
}
