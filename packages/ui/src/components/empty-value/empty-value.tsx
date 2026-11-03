import { cn } from "../../lib/utils";

/** A value that is not set (the An empty value pattern): "—" in the muted colour, never "N/A", "null" or a blank. */
export function EmptyValue({ className }: { className?: string }) {
  return (
    <span className={cn("text-muted-foreground", className)}>
      {/* A screen reader reads the dash as "em dash"; the visible sign is the whole value. */}
      —
    </span>
  );
}
