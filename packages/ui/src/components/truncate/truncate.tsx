"use client";

import { type ReactNode, useLayoutEffect, useRef, useState } from "react";
import { cn } from "../../lib/utils";
import { Tooltip, TooltipContent, TooltipTrigger } from "../ui/tooltip";

export type TruncateProps = {
  /** The text. A cut text ends with "…" and keeps its full text in a tooltip. */
  children: ReactNode;
  className?: string;
};

/**
 * Text on one line, cut at its end with "…" (the Long text pattern): menus, tabs, table cells, names and emails. Only a cut text
 * has the tooltip with its full text, on hover and on keyboard focus; then it takes a place in the tab order, so a keyboard can
 * reach the tooltip (WCAG 2.2, 1.4.13).
 */
export function Truncate({ children, className }: TruncateProps) {
  const ref = useRef<HTMLSpanElement>(null);
  const [cut, setCut] = useState(false);
  useLayoutEffect(() => {
    const element = ref.current;
    if (!element) return;
    const measure = () => setCut(element.scrollWidth > element.clientWidth);
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, []);
  const text = (
    <span
      ref={ref}
      data-truncate=""
      className={cn("block min-w-0 truncate", className)}
    >
      {children}
    </span>
  );
  if (!cut) return text;
  return (
    <Tooltip>
      <TooltipTrigger
        // biome-ignore lint/a11y/noNoninteractiveTabindex: a cut text takes focus so a keyboard can open its tooltip (WCAG 2.2, 1.4.13).
        render={<span tabIndex={0} />}
        className="block min-w-0 rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {text}
      </TooltipTrigger>
      <TooltipContent>{children}</TooltipContent>
    </Tooltip>
  );
}
