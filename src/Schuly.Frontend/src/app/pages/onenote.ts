import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideExternalLink, lucideNotebook } from '@ng-icons/lucide';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSkeletonImports } from '@spartan-ng/helm/skeleton';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { EmptyState } from '../shared/empty-state';
import { MicrosoftAccountPicker } from '../shared/microsoft-account-picker';
import { MicrosoftProblem } from '../shared/microsoft-problem';
import { NotebooksStore } from '../stores/microsoft.store';

@Component({
  selector: 'app-onenote',
  imports: [HlmTableImports, HlmBadge, HlmButton, HlmSkeletonImports, NgIcon, DatePipe, EmptyState, MicrosoftAccountPicker, MicrosoftProblem],
  providers: [provideIcons({ lucideExternalLink, lucideNotebook }), NotebooksStore],
  template: `
    <div class="flex flex-col gap-4 p-4 lg:p-6">
      <app-microsoft-account-picker />

      @if (store.waiting() || store.loading()) {
        <div class="flex flex-col gap-2">
          @for (i of [1, 2, 3]; track i) {
            <hlm-skeleton class="h-10 w-full" />
          }
        </div>
      } @else if (store.problem(); as problem) {
        <app-microsoft-problem [problem]="problem" />
      } @else if (store.notebooks().length) {
        <div class="overflow-hidden rounded-lg border">
          <div hlmTableContainer>
            <table hlmTable>
              <thead hlmTHead class="bg-muted">
                <tr hlmTr>
                  <th hlmTh>Notebook</th>
                  <th hlmTh class="hidden sm:table-cell">Last modified</th>
                  <th hlmTh class="w-12"><span class="sr-only">Open in OneNote</span></th>
                </tr>
              </thead>
              <tbody hlmTBody>
                @for (notebook of store.notebooks(); track notebook.id) {
                  <tr hlmTr>
                    <td hlmTd class="font-medium">
                      {{ notebook.name }}
                      @if (notebook.shared) {
                        <span hlmBadge variant="outline" class="ml-1">Shared</span>
                      }
                    </td>
                    <td hlmTd class="text-muted-foreground hidden tabular-nums sm:table-cell">
                      {{ notebook.modified ? (notebook.modified | date: 'd MMM y') : '-' }}
                    </td>
                    <td hlmTd>
                      @if (notebook.link) {
                        <a hlmBtn variant="ghost" size="icon-sm" [href]="notebook.link" target="_blank" rel="noopener">
                          <ng-icon name="lucideExternalLink" aria-hidden="true" />
                          <span class="sr-only">Open {{ notebook.name }} in OneNote (opens in a new tab)</span>
                        </a>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      } @else {
        <app-empty-state icon="lucideNotebook" title="No notebooks" description="Your OneNote notebooks appear here." />
      }
    </div>
  `,
})
export class OneNote {
  protected readonly store = inject(NotebooksStore);
}
