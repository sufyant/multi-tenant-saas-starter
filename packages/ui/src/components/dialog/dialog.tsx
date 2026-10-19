"use client";

import { type ReactNode, useId, useState } from "react";
import { cn } from "../../lib/utils";
import { Button } from "../button";
import { Icon, LoaderCircleIcon } from "../icon";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "../ui/alert-dialog";
import { Input as InputBase } from "../ui/input";

export type DialogProps = {
  /** The question, naming the object: "Remove Ayşe Yılmaz from Northwind Traders?" Never "Are you sure?". */
  title: ReactNode;
  /** What happens next and what stays. */
  description?: ReactNode;
  tone?: "default" | "destructive";
  /** The confirm button repeats the verb: "Remove member". */
  actionLabel: string;
  cancelLabel: string;
  /** High-risk actions: the text the user types, usually the slug, to enable the button. */
  confirmText?: string;
  /** The label of the type-to-confirm field: "Type northwind to confirm". */
  confirmLabel?: string;
  onConfirm?: () => void;
  /** The action runs: the confirm button is busy and disabled until the server answers (F47). */
  pending?: boolean;
  /** The element that opens the dialog. */
  trigger?: React.ReactElement;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  defaultOpen?: boolean;
  children?: ReactNode;
};

/**
 * A confirm step for an action that cannot be undone (the Dialog README): centred on wide screens, a bottom sheet with full-width
 * buttons under 640px, confirm above cancel. Escape and the scrim cancel.
 */
export function Dialog({
  title,
  description,
  tone = "default",
  actionLabel,
  cancelLabel,
  confirmText,
  confirmLabel,
  onConfirm,
  pending = false,
  trigger,
  open,
  onOpenChange,
  defaultOpen,
  children,
}: DialogProps) {
  const [typed, setTyped] = useState("");
  const fieldId = useId();
  const blocked = confirmText !== undefined && typed !== confirmText;
  return (
    <AlertDialog
      open={open}
      onOpenChange={onOpenChange}
      defaultOpen={defaultOpen}
    >
      {trigger && <AlertDialogTrigger render={trigger} />}
      <AlertDialogContent
        className={cn(
          "gap-4 rounded-xl p-6 shadow-lg ring-0 sm:max-w-md data-[size=default]:max-w-md",
          "max-sm:inset-x-0 max-sm:start-0 max-sm:top-auto max-sm:bottom-0 max-sm:max-w-none max-sm:translate-x-0 max-sm:translate-y-0 max-sm:rounded-b-none max-sm:rtl:translate-x-0",
        )}
      >
        <div
          aria-hidden="true"
          className="mx-auto -mt-2 h-1 w-10 rounded-full bg-border sm:hidden"
        />
        <AlertDialogHeader className="place-items-start text-start">
          <AlertDialogTitle className="text-section-title">
            {title}
          </AlertDialogTitle>
          {description && (
            <AlertDialogDescription className="text-body">
              {description}
            </AlertDialogDescription>
          )}
        </AlertDialogHeader>
        {children}
        {confirmText !== undefined && (
          <div className="flex flex-col gap-1.5">
            <label htmlFor={fieldId} className="text-body font-medium">
              {confirmLabel}
            </label>
            <InputBase
              id={fieldId}
              dir="ltr"
              autoComplete="off"
              value={typed}
              onChange={(event) => setTyped(event.target.value)}
              className="h-control-md rounded-md bg-card font-mono pointer-coarse:h-control-lg"
            />
          </div>
        )}
        <AlertDialogFooter className="mx-0 mb-0 rounded-none border-0 bg-transparent p-0 max-sm:flex-col-reverse max-sm:*:w-full">
          <AlertDialogCancel render={<Button />}>
            {cancelLabel}
          </AlertDialogCancel>
          <Button
            variant={tone === "destructive" ? "destructive" : "primary"}
            disabled={blocked || pending}
            aria-busy={pending || undefined}
            onClick={onConfirm}
            icon={
              pending ? (
                <Icon
                  icon={LoaderCircleIcon}
                  className="animate-spin motion-reduce:animate-none"
                />
              ) : undefined
            }
          >
            {actionLabel}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
