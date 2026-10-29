import { Component, input } from '@angular/core';
import { HlmBadge } from '@spartan-ng/helm/badge';

/** Small rounded pill with a coloured dot, e.g. "Unread". */
@Component({
  selector: 'app-status-pill',
  imports: [HlmBadge],
  template: `
    <span hlmBadge variant="outline" class="gap-1.5">
      <span class="size-1.5 rounded-full" [class]="accent() ? 'bg-primary' : 'bg-muted-foreground'" aria-hidden="true"></span>
      <ng-content />
    </span>
  `,
})
export class StatusPill {
  /** Accent dot for states that need attention; neutral otherwise. */
  readonly accent = input(false);
}
