import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { catchError, firstValueFrom, of } from 'rxjs';
import { MicrosoftAccountsStore } from './microsoft-accounts.store';

export type ConnectionSource = 'schulware' | 'odaorg' | 'microsoft';

export interface Connection {
  key: string;
  id: string;
  source: ConnectionSource;
  sourceLabel: string;
  title: string;
  detail: string | null;
}

interface PluginAccount {
  id: string;
  displayName?: string | null;
  schulnetzBaseUrl?: string | null;
  baseUrl?: string | null;
}

interface ConnectionsState {
  connections: Connection[];
  loading: boolean;
  busy: boolean;
  error: string | null;
}

const pluginSources = [
  { source: 'schulware', label: 'Schulnetz', path: '/api/plugins/schulware/accounts' },
  { source: 'odaorg', label: 'OdA Org', path: '/api/plugins/odaorg/accounts' },
] as const;

const host = (url?: string | null): string | null => {
  if (!url) return null;
  try {
    return new URL(url).host;
  } catch {
    return url;
  }
};

export const ConnectionsStore = signalStore(
  withState<ConnectionsState>({ connections: [], loading: false, busy: false, error: null }),
  withMethods((store, http = inject(HttpClient), microsoft = inject(MicrosoftAccountsStore)) => {
    const loadPlugin = async (source: (typeof pluginSources)[number]): Promise<Connection[]> => {
      const accounts = await firstValueFrom(
        http.get<PluginAccount[]>(source.path).pipe(catchError(() => of([] as PluginAccount[]))),
      );
      return (Array.isArray(accounts) ? accounts : []).map((account) => {
        const address = host(account.schulnetzBaseUrl ?? account.baseUrl);
        return {
          key: `${source.source}:${account.id}`,
          id: account.id,
          source: source.source,
          sourceLabel: source.label,
          title: account.displayName || address || source.label,
          detail: account.displayName ? address : null,
        };
      });
    };
    const loadMicrosoft = async (): Promise<Connection[]> => {
      await microsoft.load();
      return microsoft.accounts().flatMap((account) =>
        account.id
          ? [
              {
                key: `microsoft:${account.id}`,
                id: account.id,
                source: 'microsoft' as const,
                sourceLabel: 'Microsoft 365',
                title: account.displayName || account.email,
                detail: account.displayName ? account.email : null,
              },
            ]
          : [],
      );
    };
    const load = async () => {
      patchState(store, { loading: true });
      const groups = await Promise.all([...pluginSources.map(loadPlugin), loadMicrosoft()]);
      patchState(store, { connections: groups.flat(), loading: false });
    };

    return {
      load,
      disconnect: async (connection: Connection) => {
        patchState(store, { busy: true, error: null });
        try {
          if (connection.source === 'microsoft') {
            await microsoft.remove(connection.id);
          } else {
            const path = pluginSources.find((s) => s.source === connection.source)!.path;
            await firstValueFrom(http.delete(`${path}/${connection.id}`));
          }
          patchState(store, {
            connections: store.connections().filter((c) => c.key !== connection.key),
          });
        } catch {
          patchState(store, { error: 'Could not disconnect that account. Try again.' });
        } finally {
          patchState(store, { busy: false });
        }
      },
    };
  }),
  withHooks({ onInit: (store) => void store.load() }),
);
