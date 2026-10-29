import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideExternalLink, lucideMail } from '@ng-icons/lucide';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSkeletonImports } from '@spartan-ng/helm/skeleton';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { isToday } from '../core/dates';
import { EmptyState } from '../shared/empty-state';
import { MicrosoftAccountPicker } from '../shared/microsoft-account-picker';
import { MicrosoftProblem } from '../shared/microsoft-problem';
import { MailStore } from '../stores/microsoft.store';

@Component({
  selector: 'app-mail',
  imports: [HlmTableImports, HlmBadge, HlmButton, HlmSkeletonImports, NgIcon, DatePipe, EmptyState, MicrosoftAccountPicker, MicrosoftProblem],
  providers: [provideIcons({ lucideExternalLink, lucideMail }), MailStore],
  template: `
    <div class="flex flex-col gap-4 p-4 lg:p-6">
      <app-microsoft-account-picker />

      @if (mail.waiting() || mail.loading()) {
        <div class="flex flex-col gap-2">
          @for (i of [1, 2, 3, 4]; track i) {
            <hlm-skeleton class="h-10 w-full" />
          }
        </div>
      } @else if (mail.problem(); as problem) {
        <app-microsoft-problem [problem]="problem" />
      } @else if (mail.messages().length) {
        <div class="overflow-hidden rounded-lg border">
          <div hlmTableContainer>
            <table hlmTable>
              <thead hlmTHead class="bg-muted">
                <tr hlmTr>
                  <th hlmTh>Message</th>
                  <th hlmTh class="hidden sm:table-cell">From</th>
                  <th hlmTh class="hidden md:table-cell">Received</th>
                  <th hlmTh class="w-12"><span class="sr-only">Open in Outlook</span></th>
                </tr>
              </thead>
              <tbody hlmTBody>
                @for (message of mail.messages(); track message.id) {
                  <tr hlmTr>
                    <td hlmTd>
                      <div [class]="message.unread ? 'font-semibold' : 'font-medium'">
                        {{ message.subject }}
                        @if (message.unread) {
                          <span hlmBadge variant="secondary" class="ml-1">Unread</span>
                        }
                      </div>
                      <div class="text-muted-foreground max-w-md truncate text-xs sm:hidden">{{ message.sender }}</div>
                      @if (message.preview) {
                        <div class="text-muted-foreground max-w-md truncate text-xs">{{ message.preview }}</div>
                      }
                    </td>
                    <td hlmTd class="hidden sm:table-cell">{{ message.sender }}</td>
                    <td hlmTd class="text-muted-foreground hidden tabular-nums md:table-cell">
                      @if (today(message.received)) {
                        {{ message.received | date: 'HH:mm' }}
                      } @else {
                        {{ message.received | date: 'd MMM y' }}
                      }
                    </td>
                    <td hlmTd>
                      @if (message.link) {
                        <a hlmBtn variant="ghost" size="icon-sm" [href]="message.link" target="_blank" rel="noopener">
                          <ng-icon name="lucideExternalLink" aria-hidden="true" />
                          <span class="sr-only">Open {{ message.subject }} in Outlook (opens in a new tab)</span>
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
        <app-empty-state icon="lucideMail" title="No messages" description="Your latest inbox messages appear here." />
      }
    </div>
  `,
})
export class Mail {
  protected readonly mail = inject(MailStore);
  protected readonly today = isToday;
}
