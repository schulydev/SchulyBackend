import { Component, computed, input } from '@angular/core';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { AgendaEntryType } from '../api';

const labels: Record<AgendaEntryType, string> = { Event: 'Event', Lesson: 'Lesson', Test: 'Test', Holiday: 'Holiday' };

@Component({
  selector: 'app-agenda-type-badge',
  imports: [HlmBadge],
  template: `<span hlmBadge [variant]="variant()">{{ label() }}</span>`,
})
export class AgendaTypeBadge {
  readonly type = input.required<AgendaEntryType>();
  protected readonly label = computed(() => labels[this.type()]);
  protected readonly variant = computed(() => (this.type() === 'Test' ? 'default' : this.type() === 'Holiday' ? 'secondary' : 'outline'));
}
