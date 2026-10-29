"use client";

import { Switch as SwitchPrimitive } from "@base-ui/react/switch";
import { cn } from "../../lib/utils";

/**
 * A switch from shadcn on Base UI with our tokens: a 36 by 20 track, primary when on (the design system's `.st-switch`). The hit
 * area reaches 44px on touch. A reason of a disabled switch is its description (`aria-describedby`), as for `Checkbox` (F52).
 */
function Switch({ className, ...props }: SwitchPrimitive.Root.Props) {
  return (
    <SwitchPrimitive.Root
      nativeButton
      render={<button type="button" />}
      data-slot="switch"
      className={cn(
        "peer relative inline-flex h-5 w-9 shrink-0 items-center rounded-full bg-input transition-colors outline-none after:absolute after:-inset-x-1 after:-inset-y-3 focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50 data-checked:bg-primary motion-reduce:transition-none",
        className,
      )}
      {...props}
    >
      <SwitchPrimitive.Thumb
        data-slot="switch-thumb"
        className="pointer-events-none block size-4 translate-x-0.5 rounded-full bg-card shadow-xs transition-transform data-checked:translate-x-[1.125rem] motion-reduce:transition-none rtl:-translate-x-0.5 rtl:data-checked:-translate-x-[1.125rem]"
      />
    </SwitchPrimitive.Root>
  );
}

export { Switch };
