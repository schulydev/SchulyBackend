import { DOCUMENT, computed, effect, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';

export type Theme = 'light' | 'dark' | 'system';

const storageKey = 'schuly-theme';

function readTheme(): Theme {
  try {
    const stored = localStorage.getItem(storageKey);
    if (stored === 'light' || stored === 'dark') return stored;
  } catch {}
  return 'system';
}

export const ThemeStore = signalStore(
  { providedIn: 'root' },
  withState<{ theme: Theme; systemDark: boolean }>({ theme: readTheme(), systemDark: false }),
  withComputed(({ theme, systemDark }) => ({
    dark: computed(() => theme() === 'dark' || (theme() === 'system' && systemDark())),
  })),
  withMethods((store) => {
    const setTheme = (theme: Theme) => {
      patchState(store, { theme });
      try {
        localStorage.setItem(storageKey, theme);
      } catch {}
    };
    return {
      setTheme,
      toggle: () => setTheme(store.dark() ? 'light' : 'dark'),
    };
  }),
  withHooks((store, document = inject(DOCUMENT)) => ({
    onInit() {
      const media = document.defaultView?.matchMedia('(prefers-color-scheme: dark)');
      patchState(store, { systemDark: !!media?.matches });
      media?.addEventListener('change', (event) => patchState(store, { systemDark: event.matches }));
      effect(() => document.documentElement.classList.toggle('dark', store.dark()));
    },
  })),
);
