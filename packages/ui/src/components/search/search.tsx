"use client";

import { useEffect, useState } from "react";
import { Icon, SearchIcon } from "../icon";
import { Input as InputBase } from "../ui/input";

/** Search waits this long after the last key, so a word sends one request, not one per letter (F9). */
const searchDelay = 300;

export type SearchProps = {
  value: string;
  label: string;
  /** The API's limit of the list's `q`. */
  maxLength: number;
  onSearch: (q: string) => void;
};

/**
 * A list's search field (the Search component, 425:2566): the input with its search icon, 280 px wide from bp-tablet and the full
 * width on a phone. It sends the text 300 ms after the last key, and follows the URL on back and forward.
 */
export function Search({ value, label, maxLength, onSearch }: SearchProps) {
  const [draft, setDraft] = useState(value);
  useEffect(() => setDraft(value), [value]);
  useEffect(() => {
    if (draft === value) return;
    const timer = setTimeout(() => onSearch(draft), searchDelay);
    return () => clearTimeout(timer);
  }, [draft, value, onSearch]);
  return (
    <div className="relative w-full tablet:w-70">
      <Icon
        icon={SearchIcon}
        className="pointer-events-none absolute start-2.5 top-1/2 -translate-y-1/2 text-muted-foreground"
      />
      <InputBase
        type="search"
        aria-label={label}
        placeholder={label}
        maxLength={maxLength}
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        className="h-control-md w-full rounded-md bg-card ps-8.5 pe-2.5 text-small pointer-coarse:h-control-lg"
      />
    </div>
  );
}
