import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCalendarDays, lucideMapPin } from '@ng-icons/lucide';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmSkeletonImports } from '@spartan-ng/helm/skeleton';
import { HlmToggleGroupImports } from '@spartan-ng/helm/toggle-group';
import { AgendaEntryType } from '../api';
import { AgendaTypeBadge } from '../shared/agenda-type-badge';
import { EmptyState } from '../shared/empty-state';
import { AgendaFilter, AgendaStore } from '../stores/agenda.store';

@Component({
  selector: 'app-agenda',
  imports: [
    HlmCardImports,
    HlmToggleGroupImports,
    HlmSkeletonImports,
    NgIcon,
    DatePipe,
    AgendaTypeBadge,
    EmptyState,
  ],
  providers: [provideIcons({ lucideCalendarDays, lucideMapPin })],
  template: `
    <div class="flex flex-col gap-4 p-4 lg:p-6">
      <hlm-toggle-group
        type="single"
        variant="outline"
        size="sm"
        [nullable]="false"
        [value]="agenda.filter()"
        (valueChange)="setFilter($event)"
        aria-label="Filter agenda"
        class="self-start"
      >
        @for (option of filters; track option.value) {
          <button hlmToggleGroupItem [value]="option.value" class="px-3">{{ option.label }}</button>
        }
      </hlm-toggle-group>

      @if (agenda.loading()) {
        <div class="flex flex-col gap-3">
          @for (i of [1, 2, 3]; track i) {
            <hlm-skeleton class="h-28 w-full" />
          }
        </div>
      } @else {
        @for (day of agenda.days(); track day.key) {
          <section hlmCard class="gap-0 py-0">
            <div hlmCardHeader class="border-b px-4 py-3 sm:px-6">
              <h3 hlmCardTitle class="text-sm">{{ day.date | date: 'EEEE' }}</h3>
              <p hlmCardDescription>{{ day.date | date: 'd MMMM y' }}</p>
            </div>
            <ul class="divide-y">
              @for (entry of day.entries; track entry.id) {
                <li class="flex items-start gap-3 px-4 py-3 sm:gap-4 sm:px-6">
                  <div
                    class="text-muted-foreground w-12 shrink-0 pt-0.5 text-sm tabular-nums sm:w-24"
                  >
                    {{ entry.date | date: 'HH:mm' }}
                    @if (entry.endDate) {
                      <span class="hidden sm:inline">- {{ entry.endDate | date: 'HH:mm' }}</span>
                    }
                  </div>
                  <div class="grid flex-1 gap-1">
                    <span class="text-sm font-medium">{{ entry.title }}</span>
                    @if (entry.description) {
                      <span class="text-muted-foreground line-clamp-2 text-xs">{{
                        entry.description
                      }}</span>
                    }
                    @if (entry.place) {
                      <span class="text-muted-foreground flex items-center gap-1 text-xs">
                        <ng-icon name="lucideMapPin" size="12px" />
                        {{ entry.place }}
                      </span>
                    }
                  </div>
                  <app-agenda-type-badge [type]="entry.entryType" />
                </li>
              }
            </ul>
          </section>
        } @empty {
          <app-empty-state
            icon="lucideCalendarDays"
            title="Nothing planned"
            description="Upcoming lessons, tests and events appear here."
          />
        }
      }
    </div>
  `,
})
export class Agenda {
  protected readonly agenda = inject(AgendaStore);
  protected readonly filters: { value: AgendaFilter; label: string }[] = [
    { value: 'all', label: 'All' },
    { value: AgendaEntryType.Lesson, label: 'Lessons' },
    { value: AgendaEntryType.Test, label: 'Tests' },
    { value: AgendaEntryType.Event, label: 'Events' },
    { value: AgendaEntryType.Holiday, label: 'Holidays' },
  ];

  protected setFilter(value: unknown): void {
    if (typeof value === 'string') this.agenda.setFilter(value as AgendaFilter);
  }
}
