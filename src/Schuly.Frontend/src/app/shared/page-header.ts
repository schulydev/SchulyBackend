import { Component, input } from '@angular/core';

/** Page title on the left, compact controls (search, filter, primary action) on the right of the same row. */
@Component({
  selector: 'app-page-header',
  template: `
    <div class="flex flex-wrap items-center justify-between gap-x-4 gap-y-3">
      <h2 class="text-2xl font-semibold tracking-tight">{{ title() }}</h2>
      <div class="flex items-center gap-2">
        <ng-content />
      </div>
    </div>
  `,
})
export class PageHeader {
  readonly title = input.required<string>();
}
