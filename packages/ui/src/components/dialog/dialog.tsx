"use client";

import { type ReactNode, useCallback, useId, useState } from "react";
import { usePhone } from "../../lib/use-phone";
import { cn } from "../../lib/utils";
import { Button } from "../button";
import { Icon, LoaderCircleIcon, XIcon } from "../icon";
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
  /** wide, 480 px: a list of choices, such as Change role, or a form of several fields; else 440 px (the Dialog component, 406:2533). */
  size?: "default" | "wide";
  /** The confirm's button when the tone is default: primary, or secondary for a grant that must not invite a quick click. */
  action?: "primary" | "secondary";
  /** The confirm cannot run yet, for example before a step has found what it needs. */
  actionDisabled?: boolean;
  /** A step after the first: Back in place of Cancel, which returns to the step before and keeps the dialog open. */
  back?: { label: string; onBack: () => void };
  /** The confirm button repeats the verb: "Remove member". */
  actionLabel: string;
  /** Without it the dialog only informs: its one button closes it, and Escape and the scrim close it too. */
  cancelLabel?: string;
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

/** A field a person types into. A checkbox or a radio is a choice, and Base UI's hidden inputs are no field. */
const typedField =
  "textarea, [role=combobox], input:not([type=hidden], [type=checkbox], [type=radio], [aria-hidden=true])";

/**
 * Which phone layout the dialog takes (the Dialogs on a phone pattern): a form when it holds a field a person types into, else a
 * sheet. The dialog reads its own content, so every dialog follows the rule, and reads it again when a step changes the content.
 */
function useKind() {
  const [kind, setKind] = useState<"form" | "sheet">("sheet");
  const ref = useCallback((popup: HTMLElement | null) => {
    if (!popup) return;
    const read = () =>
      setKind(popup.querySelector(typedField) ? "form" : "sheet");
    read();
    const observer = new MutationObserver(read);
    observer.observe(popup, { childList: true, subtree: true });
    return () => observer.disconnect();
  }, []);
  return [kind, ref] as const;
}

/**
 * A dialog: a confirmation, a short choice or a form (the Dialog README, the Dialog component, 406:2533, and the Dialogs on a phone
 * pattern). From bp-tablet it is centred, 440 px wide or 480 px when wide: the title, the content in 13/18, then the footer band
 * with cancel before confirm. Under bp-tablet a form you type into is a full-screen page (Material 3, "Full-screen dialogs"):
 * × and the title at the top, the main button at the foot, which stays above the keyboard because the viewport resizes with it;
 * a short choice or a confirmation is a bottom sheet with a drag handle over the dimmed page (Apple HIG, "Sheets"), its buttons
 * stacked at full width, the main action on top. Escape and the scrim cancel.
 */
export function Dialog({
  title,
  description,
  tone = "default",
  size = "default",
  action = "primary",
  actionDisabled = false,
  back,
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
  const [kind, kindRef] = useKind();
  const phone = usePhone();
  const blocked =
    actionDisabled || (confirmText !== undefined && typed !== confirmText);
  return (
    <AlertDialog
      open={open}
      onOpenChange={onOpenChange}
      defaultOpen={defaultOpen}
    >
      {trigger && <AlertDialogTrigger render={trigger} />}
      <AlertDialogContent
        ref={kindRef}
        data-kind={kind}
        className={cn(
          "group/dialog gap-0 overflow-hidden rounded-xl p-0 shadow-lg ring-0 data-[size=default]:max-w-md",
          size === "wide"
            ? "data-[size=default]:tablet:max-w-120"
            : "data-[size=default]:tablet:max-w-110",
          // From bp-tablet it never outgrows the viewport: a column whose content scrolls between the title and the footer.
          "tablet:flex tablet:max-h-[calc(100dvh-var(--space-12))] tablet:flex-col",
          // Under bp-tablet: a sheet from the bottom edge, or a form over the whole screen.
          "max-tablet:inset-x-0 max-tablet:start-0 max-tablet:max-w-none max-tablet:translate-x-0 max-tablet:translate-y-0 max-tablet:rtl:translate-x-0",
          "max-tablet:data-[kind=sheet]:top-auto max-tablet:data-[kind=sheet]:bottom-0 max-tablet:data-[kind=sheet]:rounded-b-none",
          "max-tablet:data-[kind=form]:inset-0 max-tablet:data-[kind=form]:flex max-tablet:data-[kind=form]:h-dvh max-tablet:data-[kind=form]:flex-col max-tablet:data-[kind=form]:overflow-y-auto max-tablet:data-[kind=form]:rounded-none max-tablet:data-[kind=form]:pt-[env(safe-area-inset-top)] max-tablet:data-[kind=form]:shadow-none",
        )}
      >
        <div
          aria-hidden="true"
          data-slot="sheet-handle"
          className="mx-auto mt-2 hidden h-1 w-10 shrink-0 rounded-full bg-border max-tablet:group-data-[kind=sheet]/dialog:block"
        />
        <AlertDialogHeader className="place-items-start px-5 pt-5 pb-2 text-start">
          <div className="flex w-full items-center gap-2">
            {kind === "form" && phone && cancelLabel && (
              <AlertDialogCancel
                data-slot="dialog-close"
                aria-label={cancelLabel}
                render={<Button variant="ghost" icon={<Icon icon={XIcon} />} />}
                className="-ms-2 size-touch-target"
              />
            )}
            <AlertDialogTitle className="line-clamp-2 text-section-title">
              {title}
            </AlertDialogTitle>
          </div>
        </AlertDialogHeader>
        {/* The content: 4 px above it, so a focused field's ring stays inside the scrolling column, and 16 below. */}
        <div
          data-slot="dialog-body"
          className="flex flex-col gap-3 px-5 pt-1 pb-4 text-small tablet:min-h-0 tablet:overflow-y-auto"
        >
          {description && (
            <AlertDialogDescription className="text-small text-foreground">
              {description}
            </AlertDialogDescription>
          )}
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
        </div>
        {/* The footer band (406:2485): row-hover with a line on top, cancel first, the action last. */}
        <AlertDialogFooter className="mx-0 mb-0 shrink-0 gap-2 rounded-none border-t border-border bg-row-hover px-5 py-3 max-tablet:flex-col-reverse max-tablet:*:w-full max-tablet:group-data-[kind=sheet]/dialog:pb-[max(var(--space-3),env(safe-area-inset-bottom))] max-tablet:group-data-[kind=form]/dialog:mt-auto">
          {back ? (
            <Button variant="ghost" onClick={back.onBack}>
              {back.label}
            </Button>
          ) : (
            cancelLabel && (
              <AlertDialogCancel
                render={<Button />}
                className="max-tablet:group-data-[kind=form]/dialog:hidden"
              >
                {cancelLabel}
              </AlertDialogCancel>
            )
          )}
          <Button
            variant={tone === "destructive" ? "destructive" : action}
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
