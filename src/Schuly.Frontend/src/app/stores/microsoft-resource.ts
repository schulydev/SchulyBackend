import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, computed, effect, inject, untracked } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStoreFeature, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { Subject, map, of, switchMap, tap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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

/** Paging by growing the page: the endpoints take `top` but no cursor, so "more" asks again for a longer list. */
export interface ResourcePaging {
  /** First page size and the step each "load more" adds. */
  pageSize: number;
  /** Largest `top` the endpoint accepts. */
  max: number;
}

interface ResourceState<T> {
  items: T[];
  loading: boolean;
  problem: MicrosoftProblem | null;
  params: ResourceParams;
  /** The `top` currently requested; only meaningful for paged resources. */
  top: number;
  loadingMore: boolean;
  moreFailed: boolean;
  /** Whether the last response was full and `top` can still grow. */
  more: boolean;
  /** Result of the last successful "load more"; a new object each time so it can be announced again. */
  loadedMore: { count: number } | null;
}

interface ResourceRequest {
  id: string | null;
  params: ResourceParams;
  top: number;
  more: boolean;
}

// The plugin answers an expired Microsoft sign-in with 409; a 401 is the Schuly session and the auth interceptor handles it.
const problemOf = (error: unknown): MicrosoftProblem =>
  error instanceof HttpErrorResponse && error.status === 409
    ? 'expired'
    : error instanceof HttpErrorResponse && error.status === 404
      ? 'missing'
      : 'failed';

/**
 * Loads a list from /api/plugins/microsoft/accounts/{id}/{resource} for the selected account, reloading when the account or a query parameter changes.
 * With `paging`, `loadMore()` requests a longer list while the current items stay on screen; any reload starts again at one page.
 */
export function withMicrosoftResource<T>(resource: string, params: ResourceParams = {}, paging?: ResourcePaging) {
  const pageSize = paging?.pageSize ?? 0;
  return signalStoreFeature(
    withState<ResourceState<T>>({
      items: [],
      loading: false,
      problem: null,
      params,
      top: pageSize,
      loadingMore: false,
      moreFailed: false,
      more: false,
      loadedMore: null,
    }),
    withComputed((store) => {
      const accounts = inject(MicrosoftAccountsStore);
      return { waiting: computed(() => !accounts.loaded()), hasMore: computed(() => store.more()) };
    }),
    withMethods((store) => {
      const http = inject(HttpClient);
      const accounts = inject(MicrosoftAccountsStore);
      const requests = new Subject<ResourceRequest>();

      requests
        .pipe(
          tap((r) =>
            r.more
              ? patchState(store, { loadingMore: true, moreFailed: false })
              : patchState(store, {
                  items: [],
                  problem: null,
                  loading: !!r.id,
                  top: r.top,
                  loadingMore: false,
                  moreFailed: false,
                  more: false,
                  loadedMore: null,
                }),
          ),
          switchMap((r) => {
            if (!r.id) return of(null);
            const query = paging ? { ...r.params, top: r.top } : r.params;
            return http.get<T[]>(`/api/plugins/microsoft/accounts/${encodeURIComponent(r.id)}/${resource}`, { params: query }).pipe(
              map((items) => (Array.isArray(items) ? items : [])),
              tapResponse({
                next: (items) =>
                  patchState(store, {
                    items,
                    loading: false,
                    loadingMore: false,
                    more: !!paging && items.length >= r.top && r.top < paging.max,
                    loadedMore: r.more ? { count: Math.max(0, items.length - store.items().length) } : null,
                  }),
                error: (error) =>
                  r.more
                    ? patchState(store, { loadingMore: false, moreFailed: true, top: Math.max(pageSize, r.top - pageSize) })
                    : patchState(store, { loading: false, problem: problemOf(error) }),
              }),
            );
          }),
          takeUntilDestroyed(inject(DestroyRef)),
        )
        .subscribe();

      return {
        /** Changes one query parameter and reloads the list from the first page. */
        setParam: (key: string, value: string | number | boolean) =>
          patchState(store, { params: { ...store.params(), [key]: value } }),
        /** Reloads from the first page for the selected account. */
        reload: () => requests.next({ id: accounts.selectedId(), params: store.params(), top: pageSize, more: false }),
        /** Asks for one more page; the current items stay until the longer list arrives. */
        loadMore: () => {
          if (!paging || !store.more() || store.loading() || store.loadingMore()) return;
          const top = Math.min(store.top() + pageSize, paging.max);
          patchState(store, { top });
          requests.next({ id: accounts.selectedId(), params: store.params(), top, more: true });
        },
      };
    }),
    withHooks({
      onInit: (store) => {
        const accounts = inject(MicrosoftAccountsStore);
        effect(() => {
          accounts.selectedId();
          store.params();
          untracked(() => store.reload());
        });
      },
    }),
  );
}
