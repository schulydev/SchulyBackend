import { Component } from '@angular/core';

/** Compact page controls (search, filter, primary action), right-aligned. The page title lives in the shell's top bar. */
@Component({
  selector: 'app-page-header',
  template: `
    <div class="flex flex-wrap items-center justify-end gap-2">
      <ng-content />
    </div>
  `,
})
export class PageHeader {}
