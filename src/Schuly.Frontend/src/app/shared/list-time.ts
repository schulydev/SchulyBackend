import { DatePipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { isToday } from '../core/dates';

/** Time of day for today's items, short date otherwise. */
@Component({
  selector: 'app-list-time',
  imports: [DatePipe],
  template: `
    @if (value(); as v) {
      <time [attr.datetime]="v" class="tabular-nums whitespace-nowrap">{{ v | date: format() }}</time>
    }
  `,
})
export class ListTime {
  readonly value = input<string | null | undefined>(null);
  protected readonly format = computed(() => (isToday(this.value()) ? 'HH:mm' : 'd MMM y'));
}
