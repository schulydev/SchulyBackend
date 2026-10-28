import { HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { Observable, catchError, finalize, firstValueFrom, from, map, of, shareReplay } from 'rxjs';
import { AuthApi, TokenResponse, isChallenge } from '../core/auth-api';

export type AuthStatus = 'unknown' | 'signedIn' | 'signedOut';

interface AuthState {
  accessToken: string | null;
  status: AuthStatus;
  challenge: string | null;
}

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState<AuthState>({ accessToken: null, status: 'unknown', challenge: null }),
  withMethods((store, api = inject(AuthApi), router = inject(Router)) => {
    let refreshing: Observable<string | null> | null = null;

    const accept = (tokens: TokenResponse) => {
      saveSession(tokens);
      patchState(store, { accessToken: tokens.access_token, status: 'signedIn', challenge: null });
    };
    const clear = () => {
      saveSession(null);
      patchState(store, { accessToken: null, status: 'signedOut', challenge: null });
    };

    const refresh = (): Observable<string | null> => {
      refreshing ??= exclusive(() => firstValueFrom(api.refresh())).pipe(
        map((tokens) => {
          accept(tokens);
          return tokens.access_token;
        }),
        catchError(() => {
          clear();
          return of(null);
        }),
        finalize(() => (refreshing = null)),
        shareReplay(1),
      );
      return refreshing;
    };

    return {
      refresh,
      async restore(): Promise<void> {
        const saved = readSession();
        if (saved) patchState(store, { accessToken: saved, status: 'signedIn' });
        else await firstValueFrom(refresh());
      },
      async signIn(identifier: string, password: string): Promise<'signedIn' | 'challenge'> {
        const response = await firstValueFrom(api.login(identifier, password));
        if (isChallenge(response)) {
          patchState(store, { challenge: response.challenge });
          return 'challenge';
        }
        accept(response);
        return 'signedIn';
      },
      async verifyCode(code: string, rememberDevice: boolean): Promise<void> {
        const challenge = store.challenge();
        if (!challenge) throw new Error('No sign-in is waiting for a code.');
        accept(await firstValueFrom(api.verifyTwoFactor(challenge, code, rememberDevice)));
      },
      async register(userName: string, email: string, password: string): Promise<void> {
        accept(await firstValueFrom(api.register(userName, email, password)));
        await firstValueFrom(api.requestEmailVerification(email, password)).catch(() => undefined);
      },
      cancelChallenge(): void {
        patchState(store, { challenge: null });
      },
      async signOut(): Promise<void> {
        await firstValueFrom(api.logout()).catch(() => undefined);
        clear();
        await router.navigateByUrl('/login');
      },
      signedOutElsewhere(): void {
        clear();
        void router.navigateByUrl('/login');
      },
      signInAgain(): void {
        clear();
        void router.navigate(['/login'], { queryParams: { changed: 1 } });
      },
    };
  }),
);

export function authErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error;
    if (typeof body === 'object' && body) {
      if (Array.isArray(body.errors) && body.errors.length) return body.errors.join(' ');
      if (typeof body.error_description === 'string') return body.error_description;
    }
    if (error.status === 429) return 'Too many attempts. Wait a minute and try again.';
    if (error.status === 0) return 'Schuly is not reachable right now.';
  }
  return fallback;
}

const sessionKey = 'schuly-session';

function saveSession(tokens: TokenResponse | null): void {
  try {
    if (tokens) sessionStorage.setItem(sessionKey, JSON.stringify({ token: tokens.access_token, expiresAt: Date.now() + tokens.expires_in * 1000 }));
    else sessionStorage.removeItem(sessionKey);
  } catch {}
}

function readSession(): string | null {
  try {
    const saved = JSON.parse(sessionStorage.getItem(sessionKey) ?? 'null') as { token: string; expiresAt: number } | null;
    return saved && saved.expiresAt - Date.now() > 60_000 ? saved.token : null;
  } catch {
    return null;
  }
}

function exclusive<T>(work: () => Promise<T>): Observable<T> {
  const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined;
  return from(locks ? locks.request('schuly-refresh', work) : work());
}
