export interface EmojiMatch {
  code: string;
  char: string;
}

export interface EmojiData {
  /** shortcode -> emoji character */
  byCode: Map<string, string>;
  /** every shortcode, alphabetical, for ordered prefix and substring search */
  codes: string[];
}

/** Extra spellings people type that the GitHub preset does not carry. */
const ALIASES: Record<string, string> = {
  thumbs_up: 'thumbsup',
  thumbsdown: '-1',
  thumbs_down: '-1',
  laugh: 'joy',
  lol: 'joy',
  crying: 'sob',
  praying: 'pray',
  love: 'heart',
  flame: 'fire',
  death: 'skull',
};

let loading: Promise<EmojiData> | null = null;

/** Loads the shortcode dataset the first time it is needed, so it stays out of the main bundle. */
export function loadEmoji(): Promise<EmojiData> {
  loading ??= (async () => {
    const [compact, shortcodes] = await Promise.all([
      import('emojibase-data/en/compact.json'),
      import('emojibase-data/en/shortcodes/github.json'),
    ]);
    const chars = new Map<string, string>();
    for (const e of compact.default as unknown as { hexcode: string; unicode: string }[]) chars.set(e.hexcode, e.unicode);
    const byCode = new Map<string, string>();
    for (const [hex, value] of Object.entries(shortcodes.default as unknown as Record<string, string | string[]>)) {
      const char = chars.get(hex);
      if (!char) continue;
      for (const code of Array.isArray(value) ? value : [value]) byCode.set(code, char);
    }
    for (const [alias, target] of Object.entries(ALIASES)) {
      const char = byCode.get(target);
      if (char && !byCode.has(alias)) byCode.set(alias, char);
    }
    return { byCode, codes: [...byCode.keys()].sort() };
  })().catch((error: unknown) => {
    loading = null;
    throw error;
  });
  return loading;
}

/** Prefix matches first, then substring matches; at most `limit`. */
export function searchEmoji(data: EmojiData, query: string, limit = 8): EmojiMatch[] {
  const q = query.toLowerCase();
  if (!q) return [];
  const prefix = data.codes.filter((c) => c.startsWith(q));
  // An exact shortcode leads its prefix group.
  prefix.sort((a, b) => Number(b === q) - Number(a === q) || a.length - b.length || a.localeCompare(b));
  const rest = data.codes.filter((c) => !c.startsWith(q) && c.includes(q));
  return [...prefix, ...rest].slice(0, limit).map((code) => ({ code, char: data.byCode.get(code)! }));
}

/** The `:query` ending at the caret, when it starts a word and has at least two letters. */
export function emojiQueryAt(text: string, caret: number): { start: number; query: string } | null {
  const match = /(^|[\s([{])(:([a-z][a-z0-9_+-]+))$/i.exec(text.slice(0, caret));
  return match ? { start: caret - match[2].length, query: match[3] } : null;
}

/** A complete `:code:` that was just followed by whitespace or punctuation. */
export function emojiCompleteAt(data: EmojiData, text: string, caret: number): { start: number; end: number; char: string } | null {
  const before = text.slice(0, caret);
  const match = /(^|[\s([{])(:([a-z0-9_+-]+):)([\s.,!?;)\]}])$/i.exec(before);
  const char = match ? data.byCode.get(match[3].toLowerCase()) : undefined;
  return match && char ? { start: caret - match[2].length - 1, end: caret - 1, char } : null;
}
