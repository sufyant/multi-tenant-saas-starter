"use client";

import { type ReactNode, useId } from "react";
import { cn } from "../../lib/utils";
import { Icon, LockIcon } from "../icon";
import { Input as InputBase } from "../ui/input";
import { Tooltip, TooltipContent, TooltipTrigger } from "../ui/tooltip";

export type InputProps = Omit<React.ComponentProps<"input">, "size"> & {
  /** Always shown above the field. A placeholder is an example, never the label. */
  label: ReactNode;
  /** Help under the field. An error replaces it. */
  hint?: ReactNode;
  /** What went wrong and how to fix it. */
  error?: ReactNode;
  /** A read-only value with a lock; the reason shows on hover and focus: "Used in links. It can't be changed." */
  lockedReason?: string;
};

const leftToRight = new Set(["email", "url", "tel"]);

/** A text field with a visible label, optional help and an error (the Input README). Email, URL and ID stay left to right. */
export function Input({
  label,
  hint,
  error,
  lockedReason,
  id,
  type = "text",
  className,
  dir,
  ...props
}: InputProps) {
  const ownId = useId();
  const inputId = id ?? ownId;
  const noteId = `${inputId}-note`;
  const note = error ?? hint;
  return (
    <div className={cn("flex flex-col gap-1.5", className)}>
      <label htmlFor={inputId} className="text-body font-medium">
        {label}
      </label>
      <div className="relative">
        <InputBase
          id={inputId}
          type={type}
          dir={dir ?? (leftToRight.has(type) ? "ltr" : undefined)}
          readOnly={lockedReason !== undefined || props.readOnly}
          aria-invalid={error ? true : undefined}
          aria-describedby={note ? noteId : undefined}
          className={cn(
            "h-control-md rounded-md bg-card px-3 text-body pointer-coarse:h-control-lg tablet:pointer-coarse:h-control-xl",
            lockedReason !== undefined && "bg-muted pe-9",
          )}
          {...props}
        />
        {lockedReason !== undefined && (
          <Tooltip>
            <TooltipTrigger
              aria-label={lockedReason}
              className="absolute inset-y-0 end-0 flex w-9 items-center justify-center text-muted-foreground"
            >
              <Icon icon={LockIcon} />
            </TooltipTrigger>
            <TooltipContent>{lockedReason}</TooltipContent>
          </Tooltip>
        )}
      </div>
      {note && (
        <p
          id={noteId}
          className={cn(
            "text-small",
            error ? "text-destructive-text" : "text-muted-foreground",
          )}
        >
          {note}
        </p>
      )}
    </div>
  );
}
