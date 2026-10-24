"use client";

import { useEffect, useState } from "react";
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

/** A list's search field: it sends the text 300 ms after the last key, and follows the URL on back and forward. */
export function Search({ value, label, maxLength, onSearch }: SearchProps) {
  const [draft, setDraft] = useState(value);
  useEffect(() => setDraft(value), [value]);
  useEffect(() => {
    if (draft === value) return;
    const timer = setTimeout(() => onSearch(draft), searchDelay);
    return () => clearTimeout(timer);
  }, [draft, value, onSearch]);
  return (
    <InputBase
      type="search"
      aria-label={label}
      placeholder={label}
      maxLength={maxLength}
      value={draft}
      onChange={(event) => setDraft(event.target.value)}
      className="h-control-md w-full rounded-md bg-card px-3 text-body pointer-coarse:h-control-lg tablet:w-64"
    />
  );
}
