export type CalendarView = 'month' | 'week' | 'list';

export interface CalendarItem {
  id: string;
  sourceId: string;
  title: string;
  start: Date;
  end: Date;
  allDay: boolean;
  location: string | null;
  description: string | null;
  organizer: string | null;
  joinUrl: string | null;
  webLink: string | null;
  cancelled: boolean;
}

export interface CalendarSource {
  id: string;
  label: string;
  detail: string | null;
  color: string;
  problem: string | null;
}

export interface PlacedItem {
  item: CalendarItem;
  top: number;
  height: number;
  column: number;
  columns: number;
}

export const HOUR_HEIGHT = 48;
const MINUTE = 60_000;

export function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

export function addDays(date: Date, days: number): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + days);
}

export function addMonths(date: Date, months: number): Date {
  return new Date(date.getFullYear(), date.getMonth() + months, 1);
}

export function startOfWeek(date: Date): Date {
  const day = startOfDay(date);
  return addDays(day, -((day.getDay() + 6) % 7));
}

export function sameDay(a: Date, b: Date): boolean {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  );
}

export function daysBetween(start: Date, count: number): Date[] {
  return Array.from({ length: count }, (_, i) => addDays(start, i));
}

export function visibleRange(view: CalendarView, anchor: Date): { start: Date; end: Date } {
  if (view === 'month') {
    const start = startOfWeek(new Date(anchor.getFullYear(), anchor.getMonth(), 1));
    return { start, end: addDays(start, 42) };
  }
  if (view === 'week') {
    const start = startOfWeek(anchor);
    return { start, end: addDays(start, 7) };
  }
  const start = startOfDay(anchor);
  return { start, end: addDays(start, 28) };
}

export function overlapsDay(item: CalendarItem, day: Date): boolean {
  const dayStart = startOfDay(day);
  const dayEnd = addDays(dayStart, 1);
  return (
    item.start < dayEnd &&
    (item.end > dayStart || (item.end.getTime() === item.start.getTime() && item.start >= dayStart))
  );
}

export function parseDate(value: string, allDay: boolean): Date {
  if (allDay && /^\d{4}-\d{2}-\d{2}$/.test(value)) {
    const [y, m, d] = value.split('-').map(Number);
    return new Date(y, m - 1, d);
  }
  return new Date(value);
}

export function dateOnly(value: string): Date | null {
  const date = new Date(value);
  if (/^\d{4}-\d{2}-\d{2}$/.test(value)) return parseDate(value, true);
  return date.getUTCHours() === 0 && date.getUTCMinutes() === 0 && date.getUTCSeconds() === 0
    ? new Date(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate())
    : null;
}

export function placeTimedItems(items: CalendarItem[], day: Date, firstHour: number): PlacedItem[] {
  const dayStart = startOfDay(day);
  const origin = dayStart.getTime() + firstHour * 60 * MINUTE;
  const dayEnd = addDays(dayStart, 1).getTime();
  const sorted = [...items].sort(
    (a, b) => a.start.getTime() - b.start.getTime() || b.end.getTime() - a.end.getTime(),
  );

  const placed: PlacedItem[] = [];
  let cluster: PlacedItem[] = [];
  let clusterEnd = 0;
  const columnEnds: number[] = [];

  const closeCluster = () => {
    for (const entry of cluster) entry.columns = columnEnds.length;
    cluster = [];
    columnEnds.length = 0;
  };

  for (const item of sorted) {
    const start = Math.max(item.start.getTime(), origin);
    const end = Math.max(Math.min(item.end.getTime(), dayEnd), start + 15 * MINUTE);
    if (cluster.length && start >= clusterEnd) closeCluster();

    let column = columnEnds.findIndex((columnEnd) => columnEnd <= start);
    if (column === -1) {
      column = columnEnds.length;
      columnEnds.push(end);
    } else {
      columnEnds[column] = end;
    }

    const entry: PlacedItem = {
      item,
      top: ((start - origin) / MINUTE / 60) * HOUR_HEIGHT,
      height: ((end - start) / MINUTE / 60) * HOUR_HEIGHT,
      column,
      columns: 1,
    };
    cluster.push(entry);
    placed.push(entry);
    clusterEnd = Math.max(clusterEnd, end);
  }
  closeCluster();
  return placed;
}
