import { DatePipe } from '@angular/common';
import { Component, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild, viewChildren } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArchive, lucideArrowLeft, lucideExternalLink, lucideFileText, lucideInbox, lucideMail, lucideSend, lucideTrash2 } from '@ng-icons/lucide';
import { HlmAvatarImports } from '@spartan-ng/helm/avatar';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSkeletonImports } from '@spartan-ng/helm/skeleton';
import { initials } from '../core/initials';
import { EmptyState } from '../shared/empty-state';
import { ListTime } from '../shared/list-time';
import { LoadMore } from '../shared/load-more';
import { MicrosoftAccountPicker } from '../shared/microsoft-account-picker';
import { MicrosoftProblem } from '../shared/microsoft-problem';
import { SearchField } from '../shared/search-field';
import { MailStore } from '../stores/microsoft.store';

/** Well-known folder names; SchulyMicrosoft passes `folder` to Graph me/mailFolders/{folder}/messages as is. */
const FOLDERS = [
  { id: 'inbox', label: 'Inbox', icon: 'lucideInbox' },
  { id: 'sentitems', label: 'Sent Items', icon: 'lucideSend' },
  { id: 'drafts', label: 'Drafts', icon: 'lucideFileText' },
  { id: 'archive', label: 'Archive', icon: 'lucideArchive' },
  { id: 'deleteditems', label: 'Deleted Items', icon: 'lucideTrash2' },
] as const;

@Component({
  selector: 'app-mail',
  imports: [
    HlmAvatarImports,
    HlmButton,
    HlmSkeletonImports,
    NgIcon,
    DatePipe,
    EmptyState,
    ListTime,
    LoadMore,
    MicrosoftAccountPicker,
    MicrosoftProblem,
    SearchField,
  ],
  providers: [
    provideIcons({ lucideArchive, lucideArrowLeft, lucideExternalLink, lucideFileText, lucideInbox, lucideMail, lucideSend, lucideTrash2 }),
    MailStore,
  ],
  host: { '(keydown.escape)': 'onEscape($event)' },
  template: `
    <div class="flex h-[calc(100svh-var(--header-height,3.0625rem))] min-h-0 flex-col">
      <div class="px-4 has-[label]:pt-4 lg:px-6"><app-microsoft-account-picker /></div>

      <div class="flex min-h-0 flex-1 flex-col xl:flex-row">
        <nav
          class="flex shrink-0 gap-1 overflow-x-auto border-b p-2 xl:w-56 xl:flex-col xl:overflow-visible xl:border-e xl:border-b-0 xl:p-3"
          [class.max-lg:hidden]="selected()"
          aria-label="Mail folders"
        >
          @for (f of folders; track f.id) {
            <button
              type="button"
              class="hover:bg-muted/60 aria-[current=true]:bg-muted aria-[current=true]:font-semibold focus-visible:outline-ring relative flex shrink-0 items-center gap-3 rounded-md px-3 py-2 text-sm focus-visible:outline-2 focus-visible:-outline-offset-2"
              [attr.aria-current]="f.id === mail.folder() ? 'true' : null"
              (click)="chooseFolder(f.id)"
            >
              @if (f.id === mail.folder()) {
                <span class="bg-primary absolute inset-y-1.5 start-0 w-0.75 rounded-e-full max-xl:hidden" aria-hidden="true"></span>
              }
              <ng-icon [name]="f.icon" class="text-base" aria-hidden="true" />
              {{ f.label }}
            </button>
          }
        </nav>

        <div class="flex min-h-0 min-w-0 flex-1">
          <section
            class="flex min-h-0 w-full flex-col lg:w-96 lg:shrink-0 lg:border-e"
            [class.max-lg:hidden]="selected()"
            [attr.aria-label]="folderLabel()"
          >
            @if (!mail.problem()) {
              <div class="flex flex-col gap-2 px-4 pt-4 pb-2">
                <div class="flex items-center justify-between gap-2">
                  <h2 class="text-lg font-semibold">{{ folderLabel() }}</h2>
                  <button
                    hlmBtn
                    type="button"
                    size="sm"
                    [variant]="mail.unreadOnly() ? 'secondary' : 'ghost'"
                    [attr.aria-pressed]="mail.unreadOnly()"
                    (click)="mail.setUnreadOnly(!mail.unreadOnly())"
                  >
                    Unread
                  </button>
                </div>
                <app-search-field class="[&_hlm-input-group]:w-full" [(value)]="query" label="Search mail" placeholder="Search mail" />
                @if (query().trim() && mail.hasMore()) {
                  <p class="text-muted-foreground text-xs">Searching loaded items only. Scroll to load more.</p>
                }
              </div>
            }

            <div class="min-h-0 flex-1 overflow-y-auto">
              @if (mail.waiting() || mail.loading()) {
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
              } @else if (mail.problem(); as problem) {
                <div class="p-3"><app-microsoft-problem [problem]="problem" /></div>
              } @else if (filtered().length) {
                <ul [attr.aria-busy]="mail.loadingMore()">
                  @for (message of filtered(); track message.id) {
                    <li>
                      <button
                        #row
                        type="button"
                        class="hover:bg-muted/60 aria-[current=true]:bg-muted focus-visible:outline-ring relative flex w-full items-start gap-3 border-b py-3 ps-4 pe-3 text-left focus-visible:outline-2 focus-visible:-outline-offset-2"
                        [attr.data-id]="message.id"
                        [attr.aria-current]="message.id === selectedId() ? 'true' : null"
                        (click)="open(message.id)"
                      >
                        @if (message.unread) {
                          <span class="bg-primary absolute inset-y-0 start-0 w-1" aria-hidden="true"></span>
                        }
                        <hlm-avatar size="lg">
                          <span hlmAvatarFallback>{{ letters(message.listName) }}</span>
                        </hlm-avatar>
                        <span class="min-w-0 flex-1">
                          <span class="flex items-baseline gap-2">
                            <span class="min-w-0 flex-1 truncate text-sm" [class]="message.unread ? 'text-foreground font-bold' : 'font-medium'">
                              {{ message.listName }}
                            </span>
                            <span class="text-muted-foreground shrink-0 text-xs"><app-list-time [value]="message.received" [weekday]="true" /></span>
                          </span>
                          <span class="block truncate text-sm" [class]="message.unread ? 'text-foreground font-semibold' : ''">
                            {{ message.subject }}
                            @if (message.unread) {
                              <span class="sr-only">(unread)</span>
                            }
                          </span>
                          @if (message.preview) {
                            <span class="text-muted-foreground block truncate text-xs">{{ message.preview }}</span>
                          }
                        </span>
                      </button>
                    </li>
                  }
                </ul>
              } @else if (mail.messages().length) {
                <app-empty-state icon="lucideMail" title="No matching messages" description="Try a different search." />
              } @else {
                <app-empty-state
                  icon="lucideMail"
                  [title]="mail.unreadOnly() ? 'No unread messages' : 'No messages'"
                  [description]="mail.unreadOnly() ? 'You are all caught up.' : 'Messages in this folder appear here.'"
                />
              }
              <app-load-more
                label="Load more messages"
                [hasMore]="mail.hasMore()"
                [loading]="mail.loadingMore()"
                [failed]="mail.moreFailed()"
                [loaded]="mail.loadedMore()"
                (more)="mail.loadMore()"
              />
            </div>
          </section>

          <section class="min-w-0 flex-1 flex-col overflow-y-auto" [class]="selected() ? 'flex' : 'hidden lg:flex'" aria-label="Message">
            @if (selected(); as message) {
              <div class="flex flex-col gap-4 p-4 lg:p-6">
                <button hlmBtn variant="ghost" size="sm" class="-ms-2 self-start lg:hidden" (click)="close()">
                  <ng-icon name="lucideArrowLeft" aria-hidden="true" />
                  Back to {{ folderLabel() }}
                </button>
                <h3 #title tabindex="-1" class="text-2xl leading-tight font-semibold tracking-tight outline-none">
                  {{ message.subject }}
                </h3>
                <div class="flex items-start gap-3">
                  <hlm-avatar size="lg">
                    <span hlmAvatarFallback>{{ letters(message.sender) }}</span>
                  </hlm-avatar>
                  <div class="min-w-0 flex-1 text-sm">
                    <p class="font-semibold">{{ message.sender }}</p>
                    @if (message.address && message.address !== message.sender) {
                      <p class="text-muted-foreground truncate">{{ message.address }}</p>
                    }
                    @if (message.to.length) {
                      <p class="text-muted-foreground truncate">To: {{ message.to.join(', ') }}</p>
                    }
                  </div>
                  @if (message.received) {
                    <time class="text-muted-foreground shrink-0 text-xs" [attr.datetime]="message.received">
                      {{ message.received | date: 'EEE d MMM y, HH:mm' }}
                    </time>
                  }
                </div>
                <div class="flex flex-wrap items-center gap-3 border-y py-3">
                  @if (message.link) {
                    <a hlmBtn size="sm" [href]="message.link" target="_blank" rel="noopener">
                      Open in Outlook
                      <ng-icon name="lucideExternalLink" aria-hidden="true" />
                      <span class="sr-only">(opens in a new tab)</span>
                    </a>
                  }
                  <p class="text-muted-foreground text-xs">Showing a preview. Open in Outlook for the full message.</p>
                </div>
                <p class="text-sm leading-relaxed whitespace-pre-line">{{ message.preview || 'No preview available.' }}</p>
              </div>
            } @else {
              <div class="text-muted-foreground flex flex-1 flex-col items-center justify-center gap-2 text-sm">
                <ng-icon name="lucideMail" class="text-2xl" aria-hidden="true" />
                Select a message to read it
              </div>
            }
          </section>
        </div>
      </div>
    </div>
  `,
})
export class Mail {
  protected readonly mail = inject(MailStore);
  private readonly injector = inject(Injector);

  protected readonly folders = FOLDERS;
  protected readonly letters = (name: string) => initials(name) || '?';
  protected readonly query = signal('');
  protected readonly selectedId = signal<string | null>(null);
  private readonly title = viewChild<ElementRef<HTMLElement>>('title');
  private readonly rows = viewChildren<ElementRef<HTMLElement>>('row');

  protected readonly folderLabel = computed(() => FOLDERS.find((f) => f.id === this.mail.folder())?.label ?? 'Mail');

  /** Sent items and drafts list who the message went to, like Outlook; other folders list the sender. */
  protected readonly items = computed(() => {
    const outgoing = this.mail.folder() === 'sentitems' || this.mail.folder() === 'drafts';
    return this.mail.messages().map((m) => ({ ...m, listName: outgoing && m.to.length ? m.to.join(', ') : m.sender }));
  });

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    if (!q) return this.items();
    return this.items().filter((m) => [m.sender, m.address, m.subject, m.preview, ...m.to].some((field) => field?.toLowerCase().includes(q)));
  });

  protected readonly selected = computed(() => this.filtered().find((m) => m.id === this.selectedId()) ?? null);

  protected chooseFolder(id: string): void {
    if (id === this.mail.folder()) return;
    this.selectedId.set(null);
    this.mail.setFolder(id);
  }

  protected open(id: string): void {
    this.selectedId.set(id);
    afterNextRender(() => this.title()?.nativeElement.focus(), { injector: this.injector });
  }

  protected close(): void {
    const id = this.selectedId();
    this.selectedId.set(null);
    afterNextRender(() => this.rows().find((r) => r.nativeElement.dataset['id'] === id)?.nativeElement.focus(), {
      injector: this.injector,
    });
  }

  protected onEscape(event: Event): void {
    if (this.selected()) {
      event.preventDefault();
      this.close();
    }
  }
}
