import { signal } from '@angular/core';

const read = (key: string): number | null => {
  try {
    const value = Number(localStorage.getItem(key));
    return Number.isFinite(value) && value > 0 ? value : null;
  } catch {
    return null;
  }
};

/** A number signal remembered in localStorage; storage may be unavailable, then it is just a signal. */
export function persistedNumber(key: string, fallback: number) {
  const value = signal(read(key) ?? fallback);
  return {
    value,
    set(next: number): void {
      value.set(next);
      try {
        localStorage.setItem(key, String(Math.round(next)));
      } catch {
        // Not remembered; the width still applies for this visit.
      }
    },
  };
}
