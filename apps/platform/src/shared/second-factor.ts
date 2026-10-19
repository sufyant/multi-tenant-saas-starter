/**
 * Whether the session verified a second factor (A6): Clerk's `factorVerificationAge` holds the minutes since the first and the
 * second factor were verified, and -1 means never. The API reads the token's `fva` the same way.
 */
export function hasVerifiedSecondFactor(
  factorVerificationAge: readonly [number, number] | null | undefined,
): boolean {
  return (factorVerificationAge?.[1] ?? -1) >= 0;
}
