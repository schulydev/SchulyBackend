import { computed, inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { pipe, switchMap, tap } from 'rxjs';
import { ApplicationUserDto, AuthService } from '../api';
import { initials } from '../core/initials';

interface UserState {
  user: ApplicationUserDto | null;
  loading: boolean;
  busy: boolean;
}

export const UserStore = signalStore(
  { providedIn: 'root' },
  withState<UserState>({ user: null, loading: false, busy: false }),
  withComputed(({ user }) => {
    const name = computed(() => {
      const schoolUser = user()?.schoolUsers?.find((s) => s.firstName);
      return (schoolUser && `${schoolUser.firstName} ${schoolUser.lastName ?? ''}`.trim()) || user()?.displayName || user()?.email || 'Schuly';
    });
    return {
      name,
      email: computed(() => user()?.email ?? ''),
      avatar: computed(() => user()?.profilePictureUrl ?? null),
      initials: computed(() => initials(name())),
      schoolCount: computed(() => user()?.schoolUsers?.length ?? 0),
    };
  }),
  withMethods((store, api = inject(AuthService), oidc = inject(OidcSecurityService)) => ({
    load: rxMethod<void>(
      pipe(
        tap(() => patchState(store, { loading: true })),
        switchMap(() =>
          api.apiAuthMeGet().pipe(
            tapResponse({
              next: (user) => patchState(store, { user, loading: false }),
              error: () => patchState(store, { loading: false }),
            }),
          ),
        ),
      ),
    ),
    logout(): void {
      oidc.logoff().subscribe();
    },
    exportData(): void {
      patchState(store, { busy: true });
      api.apiAuthMeExportGet().subscribe({
        next: (data) => {
          const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' }));
          const link = Object.assign(document.createElement('a'), { href: url, download: 'schuly-export.json' });
          link.click();
          URL.revokeObjectURL(url);
          patchState(store, { busy: false });
        },
        error: () => patchState(store, { busy: false }),
      });
    },
    deleteAccount(): void {
      patchState(store, { busy: true });
      api.apiAuthMeDelete().subscribe({
        next: () => oidc.logoffLocal(),
        error: () => patchState(store, { busy: false }),
      });
    },
  })),
  withHooks({ onInit: (store) => store.load() }),
);
