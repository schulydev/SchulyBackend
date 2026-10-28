import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap } from 'rxjs';
import {
  AgendaEntryDto,
  AgendaEntryType,
  CalendarService,
  MicrosoftCalendarAccount,
  MicrosoftCalendarEvent,
} from '../api';
import {
  CalendarItem,
  CalendarSource,
  CalendarView,
  addDays,
  addMonths,
  dateOnly,
  overlapsDay,
  parseDate,
  startOfDay,
  visibleRange,
} from '../core/calendar';
import { AgendaStore } from './agenda.store';
import { GradesStore } from './grades.store';

const HIDDEN_KEY = 'schuly-calendar-hidden';
const VIEW_KEY = 'schuly-calendar-view';

const SCHULY_SOURCES: CalendarSource[] = [
  { id: 'lessons', label: 'Lessons', detail: 'Schuly', color: 'var(--chart-2)', problem: null },
  { id: 'tests', label: 'Tests & exams', detail: 'Schuly', color: 'var(--chart-5)', problem: null },
  { id: 'events', label: 'Events', detail: 'Schuly', color: 'var(--chart-4)', problem: null },
  { id: 'holidays', label: 'Holidays', detail: 'Schuly', color: 'var(--chart-3)', problem: null },
];

const ACCOUNT_COLORS = [
  'oklch(0.6 0.19 255)',
  'oklch(0.6 0.21 300)',
  'oklch(0.62 0.15 195)',
  'oklch(0.63 0.21 350)',
  'oklch(0.64 0.16 140)',
];

const DEFAULT_DURATION = 45 * 60_000;

const AGENDA_SOURCE: Record<AgendaEntryType, string> = {
  Lesson: 'lessons',
  Test: 'tests',
  Event: 'events',
  Holiday: 'holidays',
};

interface CalendarState {
  view: CalendarView;
  anchor: Date;
  selectedDay: Date;
  selectedId: string | null;
  hidden: string[];
  accounts: MicrosoftCalendarAccount[];
  microsoftEvents: MicrosoftCalendarEvent[];
  microsoftLoading: boolean;
  microsoftAvailable: boolean;
}

function readStored<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
}

function store(key: string, value: unknown): void {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    return;
  }
}

function fromAgenda(entry: AgendaEntryDto): CalendarItem {
  const startDay = dateOnly(entry.date);
  const endDay = entry.endDate ? dateOnly(entry.endDate) : startDay;
  const allDay = startDay !== null && endDay !== null;
  const start = allDay ? startDay : new Date(entry.date);
  const end = allDay
    ? addDays(endDay, 1)
    : entry.endDate
      ? new Date(entry.endDate)
      : new Date(start.getTime() + DEFAULT_DURATION);
  return {
    id: `agenda:${entry.id ?? entry.date + entry.title}`,
    sourceId: AGENDA_SOURCE[entry.entryType],
    title: entry.title,
    start,
    end: end > start ? end : new Date(start.getTime() + DEFAULT_DURATION),
    allDay,
    location: entry.place ?? null,
    description: entry.description ?? null,
    organizer: null,
    joinUrl: null,
    webLink: null,
    cancelled: false,
  };
}

function fromMicrosoft(event: MicrosoftCalendarEvent): CalendarItem {
  return {
    id: `ms:${event.accountId}:${event.id}:${event.start}`,
    sourceId: `ms:${event.accountId}`,
    title: event.title,
    start: parseDate(event.start, event.isAllDay ?? false),
    end: parseDate(event.end, event.isAllDay ?? false),
    allDay: event.isAllDay ?? false,
    location: event.location ?? null,
    description: event.preview ?? null,
    organizer: event.organizer ?? null,
    joinUrl: event.joinUrl ?? null,
    webLink: event.webLink ?? null,
    cancelled: event.isCancelled ?? false,
  };
}

function accountProblem(account: MicrosoftCalendarAccount): string | null {
  if (account.status === 'reconnect')
    return 'Sign-in expired. Link this account again in the Schuly app.';
  if (account.status === 'unavailable')
    return account.message ?? 'Microsoft is not answering for this account right now.';
  return null;
}

export const CalendarStore = signalStore(
  withState<CalendarState>(() => ({
    view: readStored<CalendarView>(VIEW_KEY, 'month'),
    anchor: startOfDay(new Date()),
    selectedDay: startOfDay(new Date()),
    selectedId: null,
    hidden: readStored<string[]>(HIDDEN_KEY, []),
    accounts: [],
    microsoftEvents: [],
    microsoftLoading: false,
    microsoftAvailable: true,
  })),
  withComputed((state, agenda = inject(AgendaStore), grades = inject(GradesStore)) => {
    const range = computed(() => visibleRange(state.view(), state.anchor()));

    const sources = computed<CalendarSource[]>(() => [
      ...SCHULY_SOURCES,
      ...state.accounts().map((account, i) => ({
        id: `ms:${account.id}`,
        label: account.email,
        detail: account.displayName ? `${account.displayName} (${account.email})` : account.email,
        color: ACCOUNT_COLORS[i % ACCOUNT_COLORS.length],
        problem: accountProblem(account),
      })),
    ]);

    const allItems = computed<CalendarItem[]>(() => {
      const exams: CalendarItem[] = grades
        .items()
        .filter((exam) => exam.date)
        .map((exam) => {
          const day = dateOnly(exam.date!);
          const start = day ?? new Date(exam.date!);
          return {
            id: `exam:${exam.id ?? exam.name}`,
            sourceId: 'tests',
            title: exam.name,
            start,
            end: day ? addDays(day, 1) : new Date(start.getTime() + DEFAULT_DURATION),
            allDay: day !== null,
            location: null,
            description: exam.description ?? null,
            organizer: null,
            joinUrl: null,
            webLink: null,
            cancelled: false,
          };
        });
      return [
        ...agenda.items().map(fromAgenda),
        ...exams,
        ...state.microsoftEvents().map(fromMicrosoft),
      ].sort((a, b) => a.start.getTime() - b.start.getTime());
    });

    const visibleItems = computed(() => {
      const hidden = new Set(state.hidden());
      const { start, end } = range();
      return allItems().filter(
        (item) => !hidden.has(item.sourceId) && item.start < end && item.end >= start,
      );
    });

    const colors = computed(() => new Map(sources().map((source) => [source.id, source.color])));

    return {
      range,
      sources,
      visibleItems,
      colors,
      loading: computed(() => agenda.loading() || grades.loading() || state.microsoftLoading()),
      selected: computed(() => allItems().find((item) => item.id === state.selectedId()) ?? null),
      selectedDayItems: computed(() =>
        visibleItems().filter((item) => overlapsDay(item, state.selectedDay())),
      ),
      problems: computed(() => sources().filter((source) => source.problem)),
    };
  }),
  withMethods((state, api = inject(CalendarService)) => {
    const move = (direction: 1 | -1) => {
      const anchor = state.anchor();
      const view = state.view();
      const next =
        view === 'month'
          ? addMonths(anchor, direction)
          : addDays(anchor, direction * (view === 'week' ? 7 : 28));
      patchState(state, { anchor: next, selectedDay: next });
    };

    return {
      setView(view: CalendarView): void {
        patchState(state, { view, anchor: state.selectedDay() });
        store(VIEW_KEY, view);
      },
      previous: () => move(-1),
      next: () => move(1),
      today(): void {
        const today = startOfDay(new Date());
        patchState(state, { anchor: today, selectedDay: today });
      },
      selectDay(day: Date): void {
        patchState(state, { selectedDay: startOfDay(day) });
      },
      openDay(day: Date): void {
        patchState(state, { selectedDay: startOfDay(day), anchor: startOfDay(day), view: 'week' });
        store(VIEW_KEY, 'week');
      },
      select(id: string | null): void {
        patchState(state, { selectedId: id });
      },
      toggleSource(id: string): void {
        const hidden = state.hidden().includes(id)
          ? state.hidden().filter((h) => h !== id)
          : [...state.hidden(), id];
        patchState(state, { hidden });
        store(HIDDEN_KEY, hidden);
      },
      loadMicrosoft: rxMethod<{ start: Date; end: Date }>(
        pipe(
          tap(() => patchState(state, { microsoftLoading: true })),
          switchMap(({ start, end }) =>
            api.apiPluginsMicrosoftCalendarGet(start.toISOString(), end.toISOString()).pipe(
              tapResponse({
                next: (feed) =>
                  patchState(state, {
                    accounts: feed.accounts ?? [],
                    microsoftEvents: feed.events ?? [],
                    microsoftLoading: false,
                    microsoftAvailable: true,
                  }),
                error: (error: unknown) =>
                  patchState(state, {
                    microsoftLoading: false,
                    microsoftEvents: [],
                    microsoftAvailable: !(
                      error instanceof HttpErrorResponse && error.status === 404
                    ),
                  }),
              }),
            ),
          ),
        ),
      ),
    };
  }),
  withHooks({
    onInit(state) {
      state.loadMicrosoft(state.range);
    },
  }),
);
