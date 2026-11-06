import { cn } from "../../lib/utils";
import { AvatarFallback, Avatar as AvatarPrimitive } from "../ui/avatar";

export type Person = {
  /** The person's one name, whole (the API's A7). */
  name: string | null;
  /** The letter when the person has no name yet, as an invited member. */
  email?: string | null;
};

/**
 * The avatar's letters: the first letter of the name's first and last word, one letter for a name of one word, else the email's
 * first letter; nothing for a deleted user, who has neither. Only the drawing reads the words: the name itself stays whole.
 */
export function initialsOf(person: Person): string {
  const words = (person.name ?? "").split(/\s+/).filter(Boolean);
  const ends = words.length > 1 ? [words[0], words.at(-1)] : words;
  return ends.length > 0
    ? ends.map((word) => word?.charAt(0)).join("")
    : (person.email ?? "").charAt(0).toLocaleUpperCase();
}

/**
 * A person's round avatar with their initials (the design's Avatar): the initials on a soft tint, 28px in a list. A class
 * sets another size; the user menu's button fills it. The name beside it says who it is, so screen readers skip it. An
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
      className={cn("size-7 rounded-full after:hidden", className)}
    >
      <AvatarFallback className="bg-role-owner-subtle text-status text-role-owner-text">
        {initialsOf(person)}
      </AvatarFallback>
    </AvatarPrimitive>
  );
}
