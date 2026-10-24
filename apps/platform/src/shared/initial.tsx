/** The square initial of an organisation (the boards): a person's avatar is round, an organisation's is square. */
export function Initial({ name }: { name: string }) {
  return (
    <span
      aria-hidden="true"
      className="flex size-8 shrink-0 items-center justify-center rounded-md bg-muted text-small font-semibold"
    >
      {name.charAt(0)}
    </span>
  );
}
