import { DatePipe } from '@angular/common';
import { Component, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild, viewChildren } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideExternalLink, lucideListFilter, lucideMail, lucideX } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';
import { HlmSkeletonImports } from '@spartan-ng/helm/skeleton';
import { EmptyState } from '../shared/empty-state';
import { LoadMore } from '../shared/load-more';
import { ListTime } from '../shared/list-time';
import { MicrosoftAccountPicker } from '../shared/microsoft-account-picker';
import { MicrosoftProblem } from '../shared/microsoft-problem';
import { PageHeader } from '../shared/page-header';
import { SearchField } from '../shared/search-field';
import { StatusPill } from '../shared/status-pill';
import { MailStore } from '../stores/microsoft.store';

@Component({
  selector: 'app-mail',
  imports: [
    HlmButton,
    HlmDropdownMenuImports,
    HlmSkeletonImports,
    NgIcon,
    DatePipe,
    EmptyState,
    ListTime,
    LoadMore,
    MicrosoftAccountPicker,
    MicrosoftProblem,
    PageHeader,
    SearchField,
    StatusPill,
  ],
  providers: [provideIcons({ lucideArrowLeft, lucideExternalLink, lucideListFilter, lucideMail, lucideX }), MailStore],
  host: { '(keydown.escape)': 'onEscape($event)' },
  template: `
    <div class="flex flex-col gap-4 p-4 lg:p-6">
      <app-microsoft-account-picker />

      <div [class]="selected() ? 'max-lg:hidden' : ''">
        <app-page-header>
          @if (!mail.problem()) {
            <app-search-field [(value)]="query" label="Search mail" placeholder="Search mail" />
            <button
              hlmBtn
              variant="outline"
              size="icon"
              class="relative"
              aria-label="Filter messages"
              [hlmDropdownMenuTrigger]="filterMenu"
              align="end"
            >
              <ng-icon name="lucideListFilter" aria-hidden="true" />
              @if (mail.unreadOnly()) {
                <span class="bg-primary absolute top-1.5 right-1.5 size-1.5 rounded-full" aria-hidden="true"></span>
                <span class="sr-only">(filter active)</span>
              }
            </button>
          }
        </app-page-header>
      </div>
      @if (query().trim() && mail.hasMore()) {
          <p class="text-muted-foreground -mt-2 text-xs">Searching loaded items only. Scroll to load more.</p>
        }

      <ng-template #filterMenu>
        <hlm-dropdown-menu class="min-w-44">
          <button hlmDropdownMenuCheckbox [checked]="mail.unreadOnly()" (triggered)="mail.setUnreadOnly(!mail.unreadOnly())">
            Unread only
            <hlm-dropdown-menu-checkbox-indicator />
          </button>
        </hlm-dropdown-menu>
      </ng-template>

      @if (mail.waiting() || mail.loading()) {
        <div class="flex flex-col gap-2">
          @for (i of [1, 2, 3, 4]; track i) {
            <hlm-skeleton class="h-14 w-full" />
          }
        </div>
      } @else if (mail.problem(); as problem) {
        <app-microsoft-problem [problem]="problem" />
      } @else if (mail.messages().length) {
        <div class="flex min-h-0 items-stretch">
          <div class="@container min-w-0 flex-1" [class]="selected() ? 'max-lg:hidden lg:max-w-[40%] lg:flex-none lg:basis-2/5' : ''">
            @if (filtered().length) {
              <div
                class="text-muted-foreground bg-muted/40 hidden grid-cols-[12rem_minmax(0,1fr)_4rem_5.5rem] gap-x-4 border-y px-3 py-2 text-xs font-medium @2xl:grid"
                aria-hidden="true"
              >
                <span>From</span>
                <span>Message</span>
                <span class="text-right">Received</span>
                <span>Status</span>
              </div>
              <ul class="divide-y border-b" [attr.aria-busy]="mail.loadingMore()">
                @for (message of filtered(); track message.id) {
                  <li>
                    <button
                      #row
                      type="button"
                      class="hover:bg-muted/50 aria-[current=true]:bg-muted focus-visible:outline-ring grid w-full grid-cols-[minmax(0,1fr)_auto] items-center gap-x-4 gap-y-1 px-3 py-4 text-left focus-visible:outline-2 focus-visible:-outline-offset-2 @2xl:grid-cols-[12rem_minmax(0,1fr)_4rem_5.5rem]"
                      [attr.data-id]="message.id"
                      [attr.aria-current]="message.id === selectedId() ? 'true' : null"
                      (click)="open(message.id)"
                    >
                      <span class="col-start-1 row-start-1 truncate text-sm" [class]="message.unread ? 'font-semibold' : 'font-medium'">
                        {{ message.sender }}
                      </span>
                      <span class="text-muted-foreground col-start-2 row-start-1 text-right text-xs @2xl:col-start-3">
                        <app-list-time [value]="message.received" />
                      </span>
                      <span class="col-start-1 row-start-2 min-w-0 @2xl:col-start-2 @2xl:row-start-1">
                        <span class="block truncate text-sm" [class.font-semibold]="message.unread">{{ message.subject }}</span>
                        @if (message.preview) {
                          <span class="text-muted-foreground block truncate text-xs">{{ message.preview }}</span>
                        }
                      </span>
                      <span class="col-start-2 row-start-2 justify-self-end @2xl:col-start-4 @2xl:row-start-1 @2xl:justify-self-start">
                        @if (message.unread) {
                          <app-status-pill [accent]="true">Unread</app-status-pill>
                        }
                      </span>
                    </button>
                  </li>
                }
              </ul>
            } @else {
              <app-empty-state
                icon="lucideMail"
                title="No matching messages"
                description="Try a different search or clear the filter."
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

          @if (selected(); as message) {
            <section class="min-w-0 flex-1 lg:border-s lg:ps-6 lg:ms-6" aria-labelledby="mail-pane-title">
              <div class="mb-4 flex items-center justify-between gap-2">
                <button hlmBtn variant="ghost" size="sm" class="-ms-2 lg:hidden" (click)="close()">
                  <ng-icon name="lucideArrowLeft" aria-hidden="true" />
                  Back to inbox
                </button>
                <button hlmBtn variant="ghost" size="icon-sm" class="ms-auto max-lg:hidden" (click)="close()">
                  <ng-icon name="lucideX" aria-hidden="true" />
                  <span class="sr-only">Close message</span>
                </button>
              </div>
              <h3 #title id="mail-pane-title" tabindex="-1" class="text-xl font-semibold tracking-tight outline-none">
                {{ message.subject }}
              </h3>
              <dl class="mt-4 grid grid-cols-[auto_minmax(0,1fr)] gap-x-4 gap-y-1 border-y py-3 text-sm">
                <dt class="text-muted-foreground">From</dt>
                <dd class="truncate">
                  {{ message.sender }}
                  @if (message.address && message.address !== message.sender) {
                    <span class="text-muted-foreground">&lt;{{ message.address }}&gt;</span>
                  }
                </dd>
                @if (message.received) {
                  <dt class="text-muted-foreground">Date</dt>
                  <dd>{{ message.received | date: 'EEEE, d MMMM y, HH:mm' }}</dd>
                }
              </dl>
              <p class="mt-4 text-sm leading-relaxed whitespace-pre-line">{{ message.preview || 'No preview available.' }}</p>
              @if (message.link) {
                <a hlmBtn variant="outline" class="mt-6" [href]="message.link" target="_blank" rel="noopener">
                  Open in Outlook
                  <ng-icon name="lucideExternalLink" aria-hidden="true" />
                  <span class="sr-only">(opens in a new tab)</span>
                </a>
              }
            </section>
          }
        </div>
      } @else {
        <app-empty-state
          icon="lucideMail"
          [title]="mail.unreadOnly() ? 'No unread messages' : 'No messages'"
          [description]="mail.unreadOnly() ? 'You are all caught up.' : 'Your latest inbox messages appear here.'"
        />
      }
    </div>
  `,
})
export class Mail {
  protected readonly mail = inject(MailStore);
  private readonly injector = inject(Injector);

  protected readonly query = signal('');
  protected readonly selectedId = signal<string | null>(null);
  private readonly title = viewChild<ElementRef<HTMLElement>>('title');
  private readonly rows = viewChildren<ElementRef<HTMLElement>>('row');

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    if (!q) return this.mail.messages();
    return this.mail
      .messages()
      .filter((m) => [m.sender, m.address, m.subject, m.preview].some((field) => field?.toLowerCase().includes(q)));
  });

  protected readonly selected = computed(() => this.filtered().find((m) => m.id === this.selectedId()) ?? null);

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
