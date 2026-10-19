import type { ComponentProps } from "react";
import { cn } from "../../lib/utils";

/** An action that reads as a link inside a hint: "Send a new code · Use another email". */
export function TextButton({
  className,
  ...props
}: Omit<ComponentProps<"button">, "type">) {
  return (
    <button
      type="button"
      className={cn(
        "rounded-sm text-primary underline-offset-4 outline-none hover:text-primary-hover hover:underline focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50",
        className,
      )}
      {...props}
    />
  );
}
