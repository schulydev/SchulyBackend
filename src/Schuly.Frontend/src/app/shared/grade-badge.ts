import { DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { HlmBadge } from '@spartan-ng/helm/badge';

@Component({
  selector: 'app-grade-badge',
  imports: [HlmBadge, DecimalPipe],
  template: `
    <span hlmBadge [variant]="score() < 4 ? 'destructive' : 'outline'" class="tabular-nums">
      {{ score() | number: '1.1-2' }}
    </span>
  `,
})
export class GradeBadge {
  readonly score = input.required<number>();
}
