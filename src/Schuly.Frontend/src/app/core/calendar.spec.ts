import {
  CalendarItem,
  HOUR_HEIGHT,
  dateOnly,
  overlapsDay,
  parseDate,
  placeTimedItems,
  startOfWeek,
  visibleRange,
} from './calendar';

function item(id: string, start: Date, end: Date, allDay = false): CalendarItem {
  return {
    id,
    sourceId: 's',
    title: id,
    start,
    end,
    allDay,
    location: null,
    description: null,
    organizer: null,
    joinUrl: null,
    webLink: null,
    cancelled: false,
  };
}

describe('calendar dates', () => {
  it('starts weeks on Monday', () => {
    expect(startOfWeek(new Date(2026, 10, 1)).getDay()).toBe(1);
    expect(startOfWeek(new Date(2026, 10, 1)).getDate()).toBe(26);
  });

  it('shows six full weeks for a month', () => {
    const { start, end } = visibleRange('month', new Date(2026, 9, 28));
    expect(start).toEqual(new Date(2026, 8, 28));
    expect(end).toEqual(new Date(2026, 10, 9));
  });

  it('treats a UTC midnight timestamp as a whole day', () => {
    expect(dateOnly('2026-10-22T00:00:00Z')).toEqual(new Date(2026, 9, 22));
    expect(dateOnly('2026-10-22T08:20:00Z')).toBeNull();
    expect(dateOnly('2026-10-22')).toEqual(new Date(2026, 9, 22));
  });

  it('reads all-day Microsoft dates as local days', () => {
    expect(parseDate('2026-11-04', true)).toEqual(new Date(2026, 10, 4));
  });

  it('counts an all-day event on every day it covers, end exclusive', () => {
    const holiday = item('h', new Date(2026, 9, 5), new Date(2026, 9, 8), true);
    expect(overlapsDay(holiday, new Date(2026, 9, 7))).toBe(true);
    expect(overlapsDay(holiday, new Date(2026, 9, 8))).toBe(false);
  });
});

describe('placeTimedItems', () => {
  const day = new Date(2026, 10, 4);
  const at = (h: number, m = 0) => new Date(2026, 10, 4, h, m);

  it('puts overlapping events side by side', () => {
    const placed = placeTimedItems(
      [item('a', at(8), at(9)), item('b', at(8, 30), at(9, 30))],
      day,
      7,
    );
    expect(placed.map((p) => [p.item.id, p.column, p.columns])).toEqual([
      ['a', 0, 2],
      ['b', 1, 2],
    ]);
    expect(placed[0].top).toBe(HOUR_HEIGHT);
    expect(placed[0].height).toBe(HOUR_HEIGHT);
  });

  it('gives back-to-back events the full width', () => {
    const placed = placeTimedItems(
      [item('a', at(7, 40), at(8)), item('b', at(8), at(8, 45))],
      day,
      7,
    );
    expect(placed.every((p) => p.columns === 1 && p.column === 0)).toBe(true);
  });
});
