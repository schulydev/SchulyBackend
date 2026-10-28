import { Component, input } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { HlmEmptyImports } from '@spartan-ng/helm/empty';

@Component({
  selector: 'app-empty-state',
  imports: [HlmEmptyImports, NgIcon],
  template: `
    <hlm-empty class="border border-dashed">
      <hlm-empty-header>
        <hlm-empty-media variant="icon">
          <ng-icon [name]="icon()" />
        </hlm-empty-media>
        <div hlmEmptyTitle>{{ title() }}</div>
        <div hlmEmptyDescription>{{ description() }}</div>
      </hlm-empty-header>
      <ng-content />
    </hlm-empty>
  `,
})
export class EmptyState {
  readonly icon = input.required<string>();
  readonly title = input.required<string>();
  readonly description = input('');
}
