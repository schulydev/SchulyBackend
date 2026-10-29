import { Component, computed, input, model } from '@angular/core';
import { provideBrnCalendarI18n } from '@spartan-ng/brain/calendar';
import { HlmDatePickerImports, provideHlmDatePickerConfig } from '@spartan-ng/helm/date-picker';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInput } from '@spartan-ng/helm/input';
import { fromIsoDate, toIsoDate } from '../core/dates';

const LOCALE = 'en-GB';
const DATE_FORMAT = new Intl.DateTimeFormat(LOCALE, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' });
const WEEKDAYS = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];
const longMonth = (month: number, year = 2000) => new Date(year, month).toLocaleDateString(LOCALE, { month: 'long' });

/** Local date and time as the app's date picker plus a time input. `date` is yyyy-mm-dd, `time` is HH:mm. */
@Component({
  selector: 'app-date-time-field',
  imports: [HlmDatePickerImports, HlmFieldImports, HlmInput],
  providers: [
    provideHlmDatePickerConfig<Date>({
      autoCloseOnSelect: true,
      formatDate: (date) => DATE_FORMAT.format(date),
      formatInputDate: (date) => DATE_FORMAT.format(date),
    }),
    provideBrnCalendarI18n({
      firstDayOfWeek: () => 1,
      formatWeekdayName: (index) => WEEKDAYS[index],
      formatHeader: (month, year) => new Date(year, month).toLocaleDateString(LOCALE, { month: 'long', year: 'numeric' }),
      formatMonth: (month) => longMonth(month),
      formatYear: (year) => String(year),
      months: () => Array.from({ length: 12 }, (_, i) => longMonth(i)) as never,
    }),
  ],
  template: `
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <div hlmField>
        <label hlmFieldLabel [for]="id() + '-date'">{{ label() }} date<span class="sr-only">, {{ dateLabel() }}</span></label>
        <hlm-date-picker [date]="picked()" [minDate]="minimum()" [autoCloseOnSelect]="true" (dateChange)="setDate($event)">
          <hlm-date-picker-trigger [buttonId]="id() + '-date'" class="w-full">Pick a date</hlm-date-picker-trigger>
        </hlm-date-picker>
      </div>
      <div hlmField>
        <label hlmFieldLabel [for]="id() + '-time'">{{ label() }} time</label>
        <input hlmInput [id]="id() + '-time'" type="time" [value]="time()" (input)="time.set($any($event.target).value)" />
      </div>
    </div>
  `,
})
export class DateTimeField {
  readonly id = input.required<string>();
  readonly label = input('');
  readonly date = model('');
  readonly time = model('');
  /** Earliest selectable day (yyyy-mm-dd). */
  readonly min = input('');

  protected readonly picked = computed(() => fromIsoDate(this.date()));
  protected readonly minimum = computed(() => fromIsoDate(this.min()));
  protected readonly dateLabel = computed(() => {
    const d = this.picked();
    return d ? DATE_FORMAT.format(d) : 'no date chosen';
  });

  protected setDate(date: Date | null | undefined): void {
    if (date) this.date.set(toIsoDate(date));
  }
}
