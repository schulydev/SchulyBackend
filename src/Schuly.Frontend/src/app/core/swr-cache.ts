interface Entry {
  data: unknown;
  at: number;
}

const MAX_ENTRIES = 300;
const entries = new Map<string, Entry>();

/** In-memory stale-while-revalidate cache shared by Mail and Chat; keys start with the account id. Lives only as long as the tab. */
export const swrCache = {
  get<T>(key: string): { data: T; age: number } | null {
    const entry = entries.get(key);
    if (!entry) return null;
    // Re-insert so the Map keeps recently used keys last.
    entries.delete(key);
    entries.set(key, entry);
    return { data: entry.data as T, age: Date.now() - entry.at };
  },
  set(key: string, data: unknown): void {
    entries.delete(key);
    entries.set(key, { data, at: Date.now() });
    while (entries.size > MAX_ENTRIES) entries.delete(entries.keys().next().value as string);
  },
  /** Drops every entry whose key starts with the prefix. */
  invalidate(prefix: string): void {
    for (const key of [...entries.keys()]) if (key.startsWith(prefix)) entries.delete(key);
  },
  clear(): void {
    entries.clear();
  },
};
