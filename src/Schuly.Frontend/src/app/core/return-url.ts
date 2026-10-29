const key = 'schuly-return-url';

/** Only same-origin paths are accepted, so a crafted link cannot send you to another site after sign-in. */
export function safeReturnUrl(value: string | null | undefined): string | null {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) return null;
  return value;
}

export function rememberReturnUrl(value: string | null | undefined): void {
  const safe = safeReturnUrl(value);
  if (!safe) return;
  try {
    sessionStorage.setItem(key, safe);
  } catch {}
}

/** The page the user was heading to before signing in, or "/". Reads once. */
export function takeReturnUrl(fromQuery?: string | null): string {
  let stored: string | null = null;
  try {
    stored = sessionStorage.getItem(key);
    sessionStorage.removeItem(key);
  } catch {}
  return safeReturnUrl(fromQuery) ?? safeReturnUrl(stored) ?? '/';
}
