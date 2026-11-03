"use client";

import {
  cloneElement,
  type ReactElement,
  type ReactNode,
  type RefObject,
  useLayoutEffect,
  useRef,
  useState,
} from "react";
import { cn } from "../../lib/utils";
import { Tooltip, TooltipContent, TooltipTrigger } from "../ui/tooltip";

/** True while the element's text is wider than its box: the browser draws it cut, with "…". */
export function useCut(ref: RefObject<HTMLElement | null>): boolean {
  const [cut, setCut] = useState(false);
  useLayoutEffect(() => {
    const element = ref.current;
    if (!element) return;
    const measure = () => setCut(element.scrollWidth > element.clientWidth);
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, [ref]);
  return cut;
}

export type TruncateProps = {
  /** The text. A cut text ends with "…" and keeps its full text in a tooltip. */
  children: ReactNode;
  /**
   * The element that holds the text when it is a control already, such as a link of the breadcrumb: the tooltip opens from it,
   * and the text takes no tab stop of its own, so no control sits inside another (WCAG 2.2, 4.1.2).
   */
  render?: ReactElement;
  className?: string;
};

/**
 * Text on one line, cut at its end with "…" (the Long text pattern): menus, tabs, table cells, names and emails. Only a cut text
 * has the tooltip with its full text, on hover and on keyboard focus. A plain text then takes a place in the tab order, so a
 * keyboard can reach the tooltip (WCAG 2.2, 1.4.13); a control keeps its own.
 */
export function Truncate({ children, render, className }: TruncateProps) {
  const ref = useRef<HTMLSpanElement>(null);
  const cut = useCut(ref);
  const text = (
    <span
      ref={ref}
      data-truncate=""
      className={cn("block min-w-0 truncate", className)}
    >
      {children}
    </span>
  );
  if (!cut) return render ? cloneElement(render, {}, text) : text;
  return (
    <Tooltip>
      {render ? (
        <TooltipTrigger render={cloneElement(render, {}, text)} />
      ) : (
        <TooltipTrigger
          // biome-ignore lint/a11y/noNoninteractiveTabindex: a cut text takes focus so a keyboard can open its tooltip (WCAG 2.2, 1.4.13).
          render={<span tabIndex={0} />}
          className="block min-w-0 rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {text}
        </TooltipTrigger>
      )}
      <TooltipContent>{children}</TooltipContent>
    </Tooltip>
  );
}
