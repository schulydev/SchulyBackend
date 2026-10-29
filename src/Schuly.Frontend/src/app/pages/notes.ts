import { Component, computed, inject, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideExternalLink, lucideNotebook } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSkeletonImports } from '@spartan-ng/helm/skeleton';
import { EmptyState } from '../shared/empty-state';
import { ListTime } from '../shared/list-time';
import { AccountPicker } from '../shared/account-picker';
import { AccountProblem } from '../shared/account-problem';
import { PageHeader } from '../shared/page-header';
import { SearchField } from '../shared/search-field';
import { StatusPill } from '../shared/status-pill';
import { NotebooksStore } from '../stores/capability-data.store';

@Component({
  selector: 'app-notes',
  imports: [HlmButton, HlmSkeletonImports, NgIcon, EmptyState, ListTime, AccountPicker, AccountProblem, PageHeader, SearchField, StatusPill],
  providers: [provideIcons({ lucideExternalLink, lucideNotebook }), NotebooksStore],
  template: `
    <div class="flex flex-col gap-4 p-4 lg:p-6">
      <app-account-picker kind="notes" />

      <app-page-header>
        @if (!store.problem()) {
          <app-search-field [(value)]="query" label="Search notebooks" placeholder="Search notebooks" />
        }
      </app-page-header>

      @if (store.waiting() || store.loading()) {
        <div class="flex flex-col gap-2">
          @for (i of [1, 2, 3]; track i) {
            <hlm-skeleton class="h-14 w-full" />
          }
        </div>
      } @else if (store.problem(); as problem) {
        <app-account-problem [problem]="problem" />
      } @else if (filtered().length) {
        <div class="@container">
          <div
            class="text-muted-foreground bg-muted/40 hidden grid-cols-[minmax(0,1fr)_6rem_6rem_2.5rem] gap-x-4 border-y px-3 py-2 text-xs font-medium @xl:grid"
            aria-hidden="true"
          >
            <span>Notebook</span>
            <span>Access</span>
            <span class="text-right">Modified</span>
            <span></span>
          </div>
          <ul class="divide-y border-b">
            @for (notebook of filtered(); track notebook.id) {
              <li class="grid grid-cols-[minmax(0,1fr)_2.5rem] items-center gap-x-4 gap-y-1 px-3 py-4 @xl:grid-cols-[minmax(0,1fr)_6rem_6rem_2.5rem]">
                <div class="flex min-w-0 items-center gap-3">
                  <span class="bg-muted text-muted-foreground flex size-10 shrink-0 items-center justify-center rounded-lg">
                    <ng-icon name="lucideNotebook" size="18" aria-hidden="true" />
                  </span>
                  <span class="truncate text-sm font-medium">{{ notebook.name }}</span>
                </div>
                <span class="hidden @xl:block">
                  @if (notebook.shared) {
                    <app-status-pill [accent]="true">Shared</app-status-pill>
                  }
                </span>
                <span class="text-muted-foreground hidden text-right text-xs @xl:block"><app-list-time [value]="notebook.modified" /></span>
                <span class="col-start-2 row-start-1 @xl:col-auto">
                  @if (notebook.link) {
                    <a hlmBtn variant="ghost" size="icon-sm" [href]="notebook.link" target="_blank" rel="noopener">
                      <ng-icon name="lucideExternalLink" aria-hidden="true" />
                      <span class="sr-only">Open {{ notebook.name }} online (opens in a new tab)</span>
                    </a>
                  }
                </span>
              </li>
            }
          </ul>
        </div>
      } @else if (store.notebooks().length) {
        <app-empty-state icon="lucideNotebook" title="No matching notebooks" description="Try a different search." />
      } @else {
        <app-empty-state icon="lucideNotebook" title="No notebooks" description="Your notebooks appear here." />
      }
    </div>
  `,
})
export class Notes {
  protected readonly store = inject(NotebooksStore);
  protected readonly query = signal('');

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    return q ? this.store.notebooks().filter((n) => n.name.toLowerCase().includes(q)) : this.store.notebooks();
  });
}
