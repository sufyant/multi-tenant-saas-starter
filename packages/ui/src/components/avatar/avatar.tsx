import { cn } from "../../lib/utils";
import { AvatarFallback, Avatar as AvatarPrimitive } from "../ui/avatar";

export type Person = {
  firstName: string | null;
  lastName: string | null;
  /** The letter when the person has no name yet, as an invited member. */
  email?: string | null;
};

/** The avatar's letters: the first letter of each name, else of the email; nothing for a deleted user, who has neither. */
export function initialsOf(person: Person): string {
  const names = [person.firstName, person.lastName].filter(
    (name): name is string => Boolean(name),
  );
  return names.length > 0
    ? names.map((name) => name.charAt(0)).join("")
    : (person.email ?? "").charAt(0).toLocaleUpperCase();
}

/**
 * A person's round avatar with their initials (the boards). The name beside it says who it is, so screen readers skip it. An
 * organisation's initial is square and is not this component.
 */
export function Avatar({
  person,
  className,
}: {
  person: Person;
  className?: string;
}) {
  return (
    <AvatarPrimitive
      aria-hidden="true"
      className={cn("after:hidden", className)}
    >
      <AvatarFallback className="bg-muted text-small font-semibold text-foreground">
        {initialsOf(person)}
      </AvatarFallback>
    </AvatarPrimitive>
  );
}
