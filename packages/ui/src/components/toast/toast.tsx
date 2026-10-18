"use client";

import { useTheme } from "next-themes";
import type { ReactNode } from "react";
import { Toaster as Sonner, toast } from "sonner";
import { CircleCheckIcon, Icon, InfoIcon, OctagonXIcon } from "../icon";

export type ToastTone = "success" | "info" | "destructive";

export type ToastOptions = {
  /** In past tense, without "successfully": "Invitation sent". */
  title: ReactNode;
  /** A failure says why and what to do next. */
  description?: ReactNode;
  tone?: ToastTone;
  /** One optional action, such as Undo. */
  action?: { label: string; onClick: () => void };
};

/**
 * Shows a toast (the Toast README). Success and info leave after 5 seconds; a failure stays until the user closes it. Never use
 * a toast for something the user must act on: that is a Banner or a Dialog.
 */
export function showToast({
  title,
  description,
  tone = "success",
  action,
}: ToastOptions): void {
  const show = {
    success: toast.success,
    info: toast.info,
    destructive: toast.error,
  }[tone];
  show(title, {
    description,
    duration: tone === "destructive" ? Number.POSITIVE_INFINITY : 5000,
    action,
  });
}

export type ToasterProps = {
  /** The document's direction; toasts sit at the bottom end. */
  dir: "ltr" | "rtl";
  /** The accessible name of each toast's close button. */
  closeLabel: string;
  /** The accessible name of the toast region: "Notifications". */
  regionLabel: string;
};

/** The one toast region, in the root layout. */
export function Toaster({ dir, closeLabel, regionLabel }: ToasterProps) {
  const { theme = "system" } = useTheme();
  return (
    <Sonner
      theme={theme as "light" | "dark" | "system"}
      dir={dir}
      position={dir === "rtl" ? "bottom-left" : "bottom-right"}
      closeButton
      containerAriaLabel={regionLabel}
      icons={{
        success: <Icon icon={CircleCheckIcon} className="text-success" />,
        info: <Icon icon={InfoIcon} className="text-info" />,
        error: <Icon icon={OctagonXIcon} className="text-destructive" />,
      }}
      toastOptions={{
        closeButtonAriaLabel: closeLabel,
        classNames: {
          toast:
            "!bg-popover !text-popover-foreground !border-border !shadow-md !rounded-lg !font-sans",
          description: "!text-muted-foreground",
          actionButton: "!bg-transparent !text-primary !font-medium",
        },
      }}
    />
  );
}
