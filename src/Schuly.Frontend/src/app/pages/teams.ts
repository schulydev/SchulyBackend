import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideExternalLink, lucideMessagesSquare } from '@ng-icons/lucide';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSkeletonImports } from '@spartan-ng/helm/skeleton';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { isToday } from '../core/dates';
import { EmptyState } from '../shared/empty-state';
import { MicrosoftAccountPicker } from '../shared/microsoft-account-picker';
import { MicrosoftProblem } from '../shared/microsoft-problem';
import { ChatsStore } from '../stores/microsoft.store';

@Component({
  selector: 'app-teams',
  imports: [HlmTableImports, HlmBadge, HlmButton, HlmSkeletonImports, NgIcon, DatePipe, EmptyState, MicrosoftAccountPicker, MicrosoftProblem],
  providers: [provideIcons({ lucideExternalLink, lucideMessagesSquare }), ChatsStore],
  template: `
    <div class="flex flex-col gap-4 p-4 lg:p-6">
      <app-microsoft-account-picker />

      @if (store.waiting() || store.loading()) {
        <div class="flex flex-col gap-2">
          @for (i of [1, 2, 3, 4]; track i) {
            <hlm-skeleton class="h-10 w-full" />
          }
        </div>
      } @else if (store.problem(); as problem) {
        <app-microsoft-problem [problem]="problem" />
      } @else if (store.chats().length) {
        <div class="overflow-hidden rounded-lg border">
          <div hlmTableContainer>
            <table hlmTable>
              <thead hlmTHead class="bg-muted">
                <tr hlmTr>
                  <th hlmTh>Chat</th>
                  <th hlmTh class="hidden md:table-cell">Last message</th>
                  <th hlmTh class="w-12"><span class="sr-only">Open in Teams</span></th>
                </tr>
              </thead>
              <tbody hlmTBody>
                @for (chat of store.chats(); track chat.id) {
                  <tr hlmTr>
                    <td hlmTd>
                      <div class="font-medium">
                        {{ chat.title }}
                        @if (chat.kind) {
                          <span hlmBadge variant="outline" class="ml-1">{{ chat.kind }}</span>
                        }
                      </div>
                      @if (chat.preview) {
                        <div class="text-muted-foreground max-w-md truncate text-xs md:hidden">{{ chat.preview }}</div>
                      }
                    </td>
                    <td hlmTd class="text-muted-foreground hidden md:table-cell">
                      <div class="max-w-md truncate">{{ chat.preview || '-' }}</div>
                      @if (chat.updated) {
                        <div class="text-xs tabular-nums">
                          @if (today(chat.updated)) {
                            {{ chat.updated | date: 'HH:mm' }}
                          } @else {
                            {{ chat.updated | date: 'd MMM y' }}
                          }
                        </div>
                      }
                    </td>
                    <td hlmTd>
                      @if (chat.link) {
                        <a hlmBtn variant="ghost" size="icon-sm" [href]="chat.link" target="_blank" rel="noopener">
                          <ng-icon name="lucideExternalLink" aria-hidden="true" />
                          <span class="sr-only">Open {{ chat.title }} in Teams (opens in a new tab)</span>
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
        <app-empty-state icon="lucideMessagesSquare" title="No chats" description="Your recent Teams chats appear here." />
      }
    </div>
  `,
})
export class Teams {
  protected readonly store = inject(ChatsStore);
  protected readonly today = isToday;
}
