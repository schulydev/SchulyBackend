import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { AgendaEntryDto, AgendaEntryType, AgendasService } from '../api';
import { dayKey, startOfToday } from '../core/dates';
import { withCollection } from './with-collection';

export type AgendaFilter = 'all' | AgendaEntryType;

export interface AgendaDay {
  key: string;
  date: Date;
  entries: AgendaEntryDto[];
}

export const AgendaStore = signalStore(
  { providedIn: 'root' },
  withCollection<AgendaEntryDto>(() => {
    const api = inject(AgendasService);
    return () => api.apiAgendasGet();
  }),
  withState<{ filter: AgendaFilter }>({ filter: 'all' }),
  withComputed((store) => {
    const upcoming = computed(() =>
      store
        .items()
        .filter((entry) => new Date(entry.endDate ?? entry.date) >= startOfToday())
        .sort((a, b) => a.date.localeCompare(b.date)),
    );
    return {
      upcoming,
      today: computed(() => upcoming().filter((entry) => dayKey(new Date(entry.date)) === dayKey(startOfToday()))),
      days: computed<AgendaDay[]>(() => {
        const filter = store.filter();
        const days = new Map<string, AgendaDay>();
        for (const entry of upcoming()) {
          if (filter !== 'all' && entry.entryType !== filter) continue;
          const date = new Date(entry.date);
          const key = dayKey(date);
          const day = days.get(key) ?? { key, date, entries: [] };
          day.entries.push(entry);
          days.set(key, day);
        }
        return [...days.values()];
      }),
    };
  }),
  withMethods((store) => ({
    setFilter(filter: AgendaFilter): void {
      patchState(store, { filter });
    },
  })),
);
