import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStoreFeature, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { map, of, pipe, switchMap, tap } from 'rxjs';
import { MicrosoftAccountsStore } from './microsoft-accounts.store';

export type MicrosoftProblem = 'expired' | 'missing' | 'failed';

export const microsoftProblemText: Record<MicrosoftProblem, { title: string; description: string }> = {
  expired: {
    title: 'Sign-in expired',
    description: 'Your Microsoft sign-in expired. Reconnect the account in the Schuly mobile app.',
  },
  missing: {
    title: 'Account not available',
    description: 'This Microsoft account was removed or the Microsoft 365 plugin is not installed.',
  },
  failed: { title: 'Could not load data', description: 'Microsoft 365 did not respond. Try again later.' },
};

type ResourceParams = Record<string, string | number | boolean>;

interface ResourceState<T> {
  items: T[];
  loading: boolean;
  problem: MicrosoftProblem | null;
  params: ResourceParams;
}

// The plugin answers an expired Microsoft sign-in with 409; a 401 is the Schuly session and the auth interceptor handles it.
const problemOf = (error: unknown): MicrosoftProblem =>
  error instanceof HttpErrorResponse && error.status === 409
    ? 'expired'
    : error instanceof HttpErrorResponse && error.status === 404
      ? 'missing'
      : 'failed';

/** Loads a list from /api/plugins/microsoft/accounts/{id}/{resource} for the selected account, reloading when the account or a query parameter changes. */
export function withMicrosoftResource<T>(resource: string, params: ResourceParams = {}) {
  return signalStoreFeature(
    withState<ResourceState<T>>({ items: [], loading: false, problem: null, params }),
    withComputed(() => {
      const accounts = inject(MicrosoftAccountsStore);
      return { waiting: computed(() => !accounts.loaded()) };
    }),
    withMethods((store) => {
      const http = inject(HttpClient);
      return {
        /** Changes one query parameter and reloads the list. */
        setParam: (key: string, value: string | number | boolean) =>
          patchState(store, { params: { ...store.params(), [key]: value } }),
        load: rxMethod<{ id: string | null; params: ResourceParams }>(
          pipe(
            tap(({ id }) => patchState(store, { items: [], problem: null, loading: !!id })),
            switchMap(({ id, params }) =>
              id
                ? http
                    .get<T[]>(`/api/plugins/microsoft/accounts/${encodeURIComponent(id)}/${resource}`, { params })
                    .pipe(
                      map((items) => (Array.isArray(items) ? items : [])),
                      tapResponse({
                        next: (items) => patchState(store, { items, loading: false }),
                        error: (error) => patchState(store, { loading: false, problem: problemOf(error) }),
                      }),
                    )
                : of([]),
            ),
          ),
        ),
      };
    }),
    withHooks({
      onInit: (store) => {
        const accounts = inject(MicrosoftAccountsStore);
        store.load(computed(() => ({ id: accounts.selectedId(), params: store.params() })));
      },
    }),
  );
}
