import { Component, DestroyRef, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChildren } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideMessagesSquare } from '@ng-icons/lucide';
import { HlmSkeletonImports } from '@spartan-ng/helm/skeleton';
import { ChatAvatar } from '../shared/chat-avatar';
import { EmptyState } from '../shared/empty-state';
import { ListTime } from '../shared/list-time';
import { LoadMore } from '../shared/load-more';
import { MicrosoftAccountPicker } from '../shared/microsoft-account-picker';
import { MicrosoftProblem } from '../shared/microsoft-problem';
import { SearchField } from '../shared/search-field';
import { SplitHandle } from '../shared/split-handle';
import { persistedNumber } from '../core/persisted';
import { TeamsStore } from '../stores/teams.store';
import { TeamsConversation } from './teams-conversation';

const MESSAGE_POLL_MS = 15_000;
const CHAT_POLL_MS = 60_000;

@Component({
  selector: 'app-teams',
  imports: [HlmSkeletonImports, NgIcon, ChatAvatar, EmptyState, ListTime, LoadMore, MicrosoftAccountPicker, MicrosoftProblem, SearchField, SplitHandle, TeamsConversation],
  providers: [provideIcons({ lucideMessagesSquare }), TeamsStore],
  host: { class: 'flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden' },
  template: `
    <div class="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
      <div class="px-4 has-[label]:pt-4 lg:px-6"><app-microsoft-account-picker /></div>

      <div class="flex min-h-0 min-w-0 flex-1 overflow-hidden" [style.--list-w.px]="listWidth.value()">
        <section
          class="flex min-h-0 min-w-0 w-full flex-col overflow-hidden lg:w-(--list-w) lg:shrink-0"
          [class.max-lg:hidden]="store.openId()"
          aria-label="Chats"
        >
          @if (!store.problem()) {
            <div class="flex flex-col gap-2 px-4 pt-4 pb-2">
              <h2 class="text-lg font-semibold">Chats</h2>
              <app-search-field class="[&_hlm-input-group]:w-full" [(value)]="query" label="Search chats" placeholder="Search chats" />
              @if (query().trim() && store.chatsMore()) {
                <p class="text-muted-foreground text-xs">Searching loaded items only. Scroll to load more.</p>
              }
            </div>
          }
          <div class="min-h-0 flex-1 overflow-x-hidden overflow-y-auto">
            @if (store.waiting() || store.chatsLoading()) {
              <div class="flex flex-col gap-1 p-3" aria-busy="true">
                @for (i of [1, 2, 3, 4, 5]; track i) {
                  <div class="flex items-center gap-3 py-2">
                    <hlm-skeleton class="size-10 shrink-0 rounded-full" />
                    <div class="flex flex-1 flex-col gap-2">
                      <hlm-skeleton class="h-3.5 w-1/2" />
                      <hlm-skeleton class="h-3 w-4/5" />
                    </div>
                  </div>
                }
              </div>
            } @else if (store.problem(); as problem) {
              <div class="p-3"><app-microsoft-problem [problem]="problem" /></div>
            } @else if (visibleChats().length) {
              <ul [attr.aria-busy]="store.chatsLoadingMore()">
                @for (chat of visibleChats(); track chat.id) {
                  <li class="min-w-0">
                    <button
                      #row
                      type="button"
                      class="hover:bg-muted/60 aria-[current=true]:bg-muted focus-visible:outline-ring relative flex h-15 w-full min-w-0 items-center overflow-hidden gap-3 px-3 text-left focus-visible:outline-2 focus-visible:-outline-offset-2"
                      [attr.data-id]="chat.id"
                      [attr.aria-current]="chat.id === store.openId() ? 'true' : null"
                      (click)="open(chat.id)"
                      (pointerenter)="store.prefetchChat(chat.id)"
                      (focus)="store.prefetchChat(chat.id)"
                    >
                      @if (chat.id === store.openId()) {
                        <span class="bg-primary absolute inset-y-2 start-0 w-0.75 rounded-e-full" aria-hidden="true"></span>
                      }
                      <app-chat-avatar [people]="chat.people" [oneOnOne]="chat.oneOnOne" [title]="chat.title" [image]="chat.image" />
                      <span class="block min-w-0 flex-1 overflow-hidden">
                        <span class="flex min-w-0 items-baseline gap-2">
                          <span class="min-w-0 flex-1 truncate text-sm" [class]="chat.unread ? 'text-foreground font-bold' : 'font-medium'">
                            {{ chat.title }}
                          </span>
                          <span class="text-foreground/70 shrink-0 text-xs"><app-list-time [value]="chat.updated" [weekday]="true" /></span>
                        </span>
                        <span class="flex min-w-0 items-center gap-2">
                          <span
                            class="block min-w-0 flex-1 truncate text-xs"
                            [class]="chat.unread ? 'text-foreground font-semibold' : 'text-foreground/70'"
                          >
                            {{ chat.preview || 'No messages yet' }}
                          </span>
                          @if (chat.unread) {
                            <span class="bg-primary size-2 shrink-0 rounded-full" aria-hidden="true"></span>
                            <span class="sr-only">Unread</span>
                          }
                        </span>
                      </span>
                    </button>
                  </li>
                }
              </ul>
            } @else if (store.chatItems().length) {
              <app-empty-state icon="lucideMessagesSquare" title="No matching chats" description="Try a different search." />
            } @else {
              <app-empty-state icon="lucideMessagesSquare" title="No chats yet" description="Your recent Teams chats appear here." />
            }
            <app-load-more
              label="Load more chats"
              [hasMore]="store.chatsMore()"
              [loading]="store.chatsLoadingMore()"
              [failed]="store.chatsMoreFailed()"
              [loaded]="store.chatsLoadedMore()"
              (more)="store.loadMoreChats()"
            />
          </div>
        </section>

        <app-split-handle
          label="Resize chat list"
          [width]="listWidth.value()"
          [min]="240"
          [max]="560"
          [initial]="320"
          (widthChange)="listWidth.set($event)"
        />

        <section class="min-w-0 flex-1 flex-col overflow-hidden lg:border-s max-lg:w-full" [class]="store.openId() ? 'flex' : 'hidden lg:flex'" aria-label="Conversation">
          @if (store.openId()) {
            <app-teams-conversation (back)="close()" />
          } @else {
            <div class="text-muted-foreground flex flex-1 flex-col items-center justify-center gap-2 text-sm">
              <ng-icon name="lucideMessagesSquare" class="text-2xl" aria-hidden="true" />
              Select a chat to start reading
            </div>
          }
        </section>
      </div>
    </div>
  `,
})
export class Teams {
  protected readonly store = inject(TeamsStore);
  private readonly injector = inject(Injector);
  private readonly rows = viewChildren<ElementRef<HTMLElement>>('row');

  protected readonly query = signal('');
  protected readonly listWidth = persistedNumber('schuly.teams.list', 320);

  protected readonly visibleChats = computed(() => {
    const q = this.query().trim().toLowerCase();
    return this.store
      .chatItems()
      .filter((c) => !q || c.title.toLowerCase().includes(q) || c.preview.toLowerCase().includes(q))
      .sort((a, b) => (b.updated ?? '').localeCompare(a.updated ?? ''));
  });

  constructor() {
    const visible = () => document.visibilityState === 'visible';
    const messages = setInterval(() => {
      if (visible() && this.store.openId()) void this.store.refreshMessages();
    }, MESSAGE_POLL_MS);
    const chats = setInterval(() => {
      if (visible()) void this.store.loadChats(true);
    }, CHAT_POLL_MS);
    const onVisible = () => {
      if (!visible()) return;
      void this.store.loadChats(true);
      if (this.store.openId()) void this.store.refreshMessages();
    };
    document.addEventListener('visibilitychange', onVisible);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(messages);
      clearInterval(chats);
      document.removeEventListener('visibilitychange', onVisible);
    });
  }

  protected open(id: string): void {
    if (this.store.openId() === id) return;
    void this.store.open(id);
  }

  protected close(): void {
    const id = this.store.openId();
    this.store.close();
    afterNextRender(() => this.rows().find((r) => r.nativeElement.dataset['id'] === id)?.nativeElement.focus(), {
      injector: this.injector,
    });
  }
}
