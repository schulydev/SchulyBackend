const pendingKey = 'schuly-external-sign-in';

interface Pending {
  verifier: string;
  state: string;
}

export const externalErrors: Record<string, string> = {
  access_denied: 'You cancelled the sign-in.',
  account_not_found: 'No Schuly account uses that sign-in yet. Create an account first, then link it in Settings.',
  account_exists: 'An account with that email already exists. Sign in with your password, then link this provider in Settings.',
  linked_to_another_account: 'That sign-in already belongs to another Schuly account.',
  provider_already_linked: 'That provider is already linked to your account.',
  external_login_failed: 'Signing in with that provider did not work. Try again.',
};

export async function beginExternalSignIn(provider: string, returnPath: string): Promise<void> {
  const { challenge, state } = await createPending();
  const query = new URLSearchParams({ returnUrl: returnUrl(returnPath), codeChallenge: challenge, codeChallengeMethod: 'S256', state });
  location.assign(`/auth/external/${encodeURIComponent(provider)}/start?${query}`);
}

export function newLinkState(): string {
  const state = random();
  save({ verifier: '', state });
  return state;
}

export function takePending(state: string | null): Pending | null {
  let pending: Pending | null = null;
  try {
    pending = JSON.parse(sessionStorage.getItem(pendingKey) ?? 'null') as Pending | null;
    sessionStorage.removeItem(pendingKey);
  } catch {}
  return pending && state && pending.state === state ? pending : null;
}

export function returnUrl(path: string): string {
  return `${location.origin}${path}`;
}

async function createPending(): Promise<{ challenge: string; state: string }> {
  const verifier = random();
  const state = random();
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
  save({ verifier, state });
  return { challenge: base64Url(new Uint8Array(digest)), state };
}

function save(pending: Pending): void {
  try {
    sessionStorage.setItem(pendingKey, JSON.stringify(pending));
  } catch {}
}

function random(): string {
  return base64Url(crypto.getRandomValues(new Uint8Array(32)));
}

function base64Url(bytes: Uint8Array): string {
  return btoa(String.fromCharCode(...bytes)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
