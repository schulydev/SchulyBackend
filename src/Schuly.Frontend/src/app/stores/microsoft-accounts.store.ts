import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { catchError, firstValueFrom, of } from 'rxjs';
import { LinkedAccountDto, LinkedAccountsService } from '../api';

interface MicrosoftAccountsState {
  accounts: LinkedAccountDto[];
  chosenId: string | null;
  loading: boolean;
  loaded: boolean;
}

/** The signed-in user's linked Microsoft accounts, shared by the sidebar, settings and the Microsoft 365 pages. */
export const MicrosoftAccountsStore = signalStore(
  withState<MicrosoftAccountsState>({ accounts: [], chosenId: null, loading: false, loaded: false }),
  withComputed((store) => {
    const selectedId = computed(() => {
      const accounts = store.accounts();
      return accounts.find((a) => a.id === store.chosenId())?.id ?? accounts[0]?.id ?? null;
    });
    return {
      selectedId,
      selected: computed(() => store.accounts().find((a) => a.id === selectedId()) ?? null),
      hasAccounts: computed(() => store.accounts().length > 0),
    };
  }),
  withMethods((store, linked = inject(LinkedAccountsService)) => ({
    async load(): Promise<void> {
      patchState(store, { loading: true });
      const accounts = await firstValueFrom(
        linked.apiMeLinkedAccountsGet('microsoft').pipe(catchError(() => of([] as LinkedAccountDto[]))),
      );
      patchState(store, {
        accounts: (Array.isArray(accounts) ? accounts : []).filter((a) => !!a.id),
        loading: false,
        loaded: true,
      });
    },
    select(id: string): void {
      patchState(store, { chosenId: id });
    },
    async remove(id: string): Promise<void> {
      await firstValueFrom(linked.apiMeLinkedAccountsIdDelete(id));
      patchState(store, { accounts: store.accounts().filter((a) => a.id !== id) });
    },
  })),
  withHooks({ onInit: (store) => void store.load() }),
);
