import { computed, inject } from '@angular/core';
import { signalStore, withComputed } from '@ngrx/signals';
import { AgendaEntryDto, AgendasService } from '../api';
import { dayKey, startOfToday } from '../core/dates';
import { withCollection } from './with-collection';

export const AgendaStore = signalStore(
  withCollection<AgendaEntryDto>(() => {
    const api = inject(AgendasService);
    return () => api.apiAgendasGet();
  }),
  withComputed((store) => {
    const upcoming = computed(() =>
      store
        .items()
        .filter((entry) => new Date(entry.endDate ?? entry.date) >= startOfToday())
        .sort((a, b) => a.date.localeCompare(b.date)),
    );
    return {
      upcoming,
      today: computed(() =>
        upcoming().filter((entry) => dayKey(new Date(entry.date)) === dayKey(startOfToday())),
      ),
    };
  }),
);
