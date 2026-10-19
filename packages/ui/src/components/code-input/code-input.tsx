"use client";

import { REGEXP_ONLY_DIGITS } from "input-otp";
import { type ReactNode, useId } from "react";
import { InputOTP, InputOTPGroup, InputOTPSlot } from "../ui/input-otp";

const length = 6;
const slots = Array.from({ length }, (_, index) => index);

export type CodeInputProps = {
  /** Always shown above the slots: "Code". */
  label: ReactNode;
  value: string;
  onChange: (value: string) => void;
  /** Called when the sixth digit arrives, by typing or by paste. */
  onComplete?: (value: string) => void;
  /** What went wrong and how to fix it. It is announced. */
  error?: ReactNode;
  disabled?: boolean;
  autoFocus?: boolean;
  id?: string;
};

/**
 * The six-digit code of an email or an authenticator app, on shadcn's InputOTP. One input holds the code, so a paste fills every
 * slot and the browser can offer the code it read (`one-time-code`). A code reads left to right in every language, so the slots
 * are `dir="ltr"` in Arabic too.
 */
export function CodeInput({
  label,
  value,
  onChange,
  onComplete,
  error,
  disabled,
  autoFocus,
  id,
}: CodeInputProps) {
  const ownId = useId();
  const inputId = id ?? ownId;
  const errorId = `${inputId}-error`;
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={inputId} className="text-body font-medium">
        {label}
      </label>
      <InputOTP
        id={inputId}
        maxLength={length}
        pattern={REGEXP_ONLY_DIGITS}
        inputMode="numeric"
        autoComplete="one-time-code"
        value={value}
        onChange={onChange}
        onComplete={onComplete}
        disabled={disabled}
        autoFocus={autoFocus}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        containerClassName="justify-start"
      >
        <InputOTPGroup dir="ltr">
          {slots.map((index) => (
            <InputOTPSlot
              key={index}
              index={index}
              aria-invalid={error ? true : undefined}
              className="size-control-lg bg-card font-mono text-section-title"
            />
          ))}
        </InputOTPGroup>
      </InputOTP>
      {error && (
        <p
          id={errorId}
          role="alert"
          className="text-small text-destructive-text"
        >
          {error}
        </p>
      )}
    </div>
  );
}
