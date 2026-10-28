import { computed, inject } from '@angular/core';
import { signalStore, withComputed } from '@ngrx/signals';
import { ClassDto, ClassService } from '../api';
import { withCollection } from './with-collection';

export const ClassesStore = signalStore(
  { providedIn: 'root' },
  withCollection<ClassDto>(() => {
    const api = inject(ClassService);
    return () => api.apiClassGet();
  }),
  withComputed((store) => ({
    names: computed(() => new Map(store.items().map((c) => [c.id ?? '', c.name]))),
  })),
);
