import { vi } from "vitest";

/**
 * Next.js's cookie store, which exists only in a request, is not ours: the setup fakes `next/headers` with this one (Khorikov,
 * Chapter 9), so a test runs our server actions and reads what they wrote.
 */
export const cookieStore = {
  values: new Map<string, string>(),
  get: (name: string) => {
    const value = cookieStore.values.get(name);
    return value === undefined ? undefined : { name, value };
  },
  set: vi.fn((name: string, value: string) => {
    cookieStore.values.set(name, value);
  }),
  delete: vi.fn((name: string) => {
    cookieStore.values.delete(name);
  }),
};

export function resetCookies() {
  cookieStore.values.clear();
  cookieStore.set.mockClear();
  cookieStore.delete.mockClear();
}
