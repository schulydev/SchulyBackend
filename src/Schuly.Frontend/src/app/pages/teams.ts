import { Component, computed, inject, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideExternalLink, lucideMessagesSquare } from '@ng-icons/lucide';
import { HlmAvatarImports } from '@spartan-ng/helm/avatar';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSkeletonImports } from '@spartan-ng/helm/skeleton';
import { initials } from '../core/initials';
import { EmptyState } from '../shared/empty-state';
import { ListTime } from '../shared/list-time';
import { MicrosoftAccountPicker } from '../shared/microsoft-account-picker';
import { MicrosoftProblem } from '../shared/microsoft-problem';
import { PageHeader } from '../shared/page-header';
import { SearchField } from '../shared/search-field';
import { StatusPill } from '../shared/status-pill';
import { ChatsStore } from '../stores/microsoft.store';

@Component({
  selector: 'app-teams',
  imports: [HlmAvatarImports, HlmButton, HlmSkeletonImports, NgIcon, EmptyState, ListTime, MicrosoftAccountPicker, MicrosoftProblem, PageHeader, SearchField, StatusPill],
  providers: [provideIcons({ lucideExternalLink, lucideMessagesSquare }), ChatsStore],
  template: `
    <div class="flex flex-col gap-4 p-4 lg:p-6">
      <app-microsoft-account-picker />

      <app-page-header title="Teams">
        @if (!store.problem()) {
          <app-search-field [(value)]="query" label="Search chats" placeholder="Search chats" />
        }
      </app-page-header>

      @if (store.waiting() || store.loading()) {
        <div class="flex flex-col gap-2">
          @for (i of [1, 2, 3, 4]; track i) {
            <hlm-skeleton class="h-14 w-full" />
          }
        </div>
      } @else if (store.problem(); as problem) {
        <app-microsoft-problem [problem]="problem" />
      } @else if (filtered().length) {
        <div class="@container">
          <div
            class="text-muted-foreground bg-muted/40 hidden grid-cols-[2.5rem_minmax(0,1fr)_6rem_4rem_2.5rem] gap-x-4 border-y px-3 py-2 text-xs font-medium @2xl:grid"
            aria-hidden="true"
          >
            <span></span>
            <span>Chat</span>
            <span>Type</span>
            <span class="text-right">Updated</span>
            <span></span>
          </div>
          <ul class="divide-y border-b">
            @for (chat of filtered(); track chat.id) {
              <li
                class="grid grid-cols-[2.5rem_minmax(0,1fr)_auto_2.5rem] items-center gap-x-4 gap-y-1 px-3 py-4 @2xl:grid-cols-[2.5rem_minmax(0,1fr)_6rem_4rem_2.5rem]"
              >
                <hlm-avatar class="row-span-2 @2xl:row-span-1">
                  <span hlmAvatarFallback>{{ chat.initials }}</span>
                </hlm-avatar>
                <div class="min-w-0">
                  <div class="truncate text-sm font-medium">{{ chat.title }}</div>
                  @if (chat.preview) {
                    <div class="text-muted-foreground truncate text-xs">{{ chat.preview }}</div>
                  }
                </div>
                <span class="@2xl:order-none">
                  @if (chat.kind) {
                    <app-status-pill>{{ chat.kind }}</app-status-pill>
                  }
                </span>
                <span class="text-muted-foreground hidden text-right text-xs @2xl:block"><app-list-time [value]="chat.updated" /></span>
                <span class="row-span-2 @2xl:row-span-1">
                  @if (chat.link) {
                    <a hlmBtn variant="ghost" size="icon-sm" [href]="chat.link" target="_blank" rel="noopener">
                      <ng-icon name="lucideExternalLink" aria-hidden="true" />
                      <span class="sr-only">Open {{ chat.title }} in Teams (opens in a new tab)</span>
                    </a>
                  }
                </span>
              </li>
            }
          </ul>
        </div>
      } @else if (store.chats().length) {
        <app-empty-state icon="lucideMessagesSquare" title="No matching chats" description="Try a different search." />
      } @else {
        <app-empty-state icon="lucideMessagesSquare" title="No chats" description="Your recent Teams chats appear here." />
      }
    </div>
  `,
})
export class Teams {
  protected readonly store = inject(ChatsStore);
  protected readonly query = signal('');

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const chats = this.store.chats().map((c) => ({ ...c, initials: initials(c.title) || '?' }));
    return q ? chats.filter((c) => c.title.toLowerCase().includes(q) || c.preview.toLowerCase().includes(q)) : chats;
  });
}
