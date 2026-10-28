import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideDownload, lucideFileText } from '@ng-icons/lucide';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSkeletonImports } from '@spartan-ng/helm/skeleton';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { EmptyState } from '../shared/empty-state';
import { DocumentsStore } from '../stores/documents.store';

@Component({
  selector: 'app-documents',
  imports: [HlmTableImports, HlmBadge, HlmButton, HlmSkeletonImports, NgIcon, DatePipe, EmptyState],
  providers: [provideIcons({ lucideDownload, lucideFileText })],
  template: `
    <div class="flex flex-col gap-4 p-4 lg:p-6">
      @if (documents.loading()) {
        <div class="flex flex-col gap-2">
          @for (i of [1, 2, 3]; track i) {
            <hlm-skeleton class="h-10 w-full" />
          }
        </div>
      } @else if (documents.items().length) {
        <div class="overflow-hidden rounded-lg border">
          <div hlmTableContainer>
            <table hlmTable>
              <thead hlmTHead class="bg-muted">
                <tr hlmTr>
                  <th hlmTh>Document</th>
                  <th hlmTh class="hidden sm:table-cell">Category</th>
                  <th hlmTh class="hidden md:table-cell">Added</th>
                  <th hlmTh class="hidden lg:table-cell">Follow-up</th>
                  <th hlmTh class="w-12"><span class="sr-only">Download</span></th>
                </tr>
              </thead>
              <tbody hlmTBody>
                @for (document of documents.sorted(); track document.id) {
                  <tr hlmTr>
                    <td hlmTd>
                      <div class="font-medium">{{ document.title }}</div>
                      @if (document.comment) {
                        <div class="text-muted-foreground max-w-md truncate text-xs">
                          {{ document.comment }}
                        </div>
                      }
                    </td>
                    <td hlmTd class="hidden sm:table-cell">
                      @if (document.category) {
                        <span hlmBadge variant="outline">{{ document.category }}</span>
                      }
                    </td>
                    <td hlmTd class="text-muted-foreground hidden tabular-nums md:table-cell">
                      {{ document.createdAt | date: 'd MMM y' }}
                    </td>
                    <td hlmTd class="text-muted-foreground hidden lg:table-cell">
                      {{ document.followUpAction || '-' }}
                      @if (document.followUpDate) {
                        <span class="tabular-nums"
                          >({{ document.followUpDate | date: 'd MMM' }})</span
                        >
                      }
                    </td>
                    <td hlmTd>
                      @if (document.fileName) {
                        <button
                          hlmBtn
                          variant="ghost"
                          size="icon-sm"
                          (click)="documents.download(document)"
                          [attr.aria-label]="'Download ' + document.title"
                        >
                          <ng-icon name="lucideDownload" />
                        </button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      } @else {
        <app-empty-state
          icon="lucideFileText"
          title="No documents"
          description="Documents your school shares with you appear here."
        />
      }
    </div>
  `,
})
export class Documents {
  protected readonly documents = inject(DocumentsStore);
}
