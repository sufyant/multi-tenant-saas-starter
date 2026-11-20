/** The whole days from now until a moment, at least 0, a part of a day counting as a day: the days left of a trial (4.28, 10.08). */
export const daysLeft = (at: string, now = Date.now()): number =>
  Math.max(0, Math.ceil((Date.parse(at) - now) / 86_400_000));
