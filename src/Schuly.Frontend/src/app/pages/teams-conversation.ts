import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { Component, ElementRef, Injector, afterNextRender, computed, effect, inject, output, signal, untracked, viewChild } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideArrowLeft, lucideArrowUp, lucideArrowDown, lucideExternalLink, lucideMessagesSquare, lucidePaperclip } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmSkeletonImports } from '@spartan-ng/helm/skeleton';
import { HlmSpinner } from '@spartan-ng/helm/spinner';
import { dayKey } from '../core/dates';
import { renderBody } from '../core/message-html';
import { EmptyState } from '../shared/empty-state';
import { ChatAvatar } from '../shared/chat-avatar';
import { MicrosoftProblem } from '../shared/microsoft-problem';
import { PersonAvatar } from '../shared/person-avatar';
import { TeamsStore, bodyText } from '../stores/teams.store';

const GROUP_WINDOW_MS = 5 * 60 * 1000;
const NEAR_BOTTOM_PX = 80;
const MAX_LENGTH = 4000;

interface Attachment {
  name: string;
}
interface Reaction {
  label: string;
  count: number;
}
type LogItem =
  | { kind: 'day'; key: string; label: string | null; date: string }
  | { kind: 'system'; key: string; text: string }
  | {
      kind: 'msg';
      key: string;
      localId: string | null;
      mine: boolean;
      senderId: string | null;
      senderName: string;
      head: boolean;
      time: string;
      deleted: boolean;
      html: string;
      text: string;
      attachments: Attachment[];
      reactions: Reaction[];
      status: 'sent' | 'sending' | 'failed';
    };

@Component({
  selector: 'app-teams-conversation',
  imports: [DatePipe, NgTemplateOutlet, HlmButton, HlmSkeletonImports, HlmSpinner, NgIcon, ChatAvatar, EmptyState, MicrosoftProblem, PersonAvatar],
  providers: [provideIcons({ lucideArrowLeft, lucideArrowUp, lucideArrowDown, lucideExternalLink, lucideMessagesSquare, lucidePaperclip })],
  template: `
    @if (store.openChat(); as chat) {
      <header class="flex items-center gap-3 border-b px-4 py-3">
        <button hlmBtn variant="ghost" size="icon-sm" class="-ms-2 lg:hidden" (click)="back.emit()">
          <ng-icon name="lucideArrowLeft" aria-hidden="true" />
          <span class="sr-only">Back to chats</span>
        </button>
        <app-chat-avatar [people]="chat.people" [oneOnOne]="chat.oneOnOne" [title]="chat.title" />
        <h2 class="min-w-0 flex-1 truncate text-base font-medium">{{ chat.title }}</h2>
        @if (chat.link) {
          <a hlmBtn variant="ghost" size="sm" [href]="chat.link" target="_blank" rel="noopener">
            <span class="max-sm:sr-only">Open in Teams</span>
            <ng-icon name="lucideExternalLink" aria-hidden="true" />
            <span class="sr-only">(opens in a new tab)</span>
          </a>
        }
      </header>

      <div class="relative min-h-0 flex-1">
        <div
          #log
          role="log"
          aria-live="polite"
          aria-label="Messages"
          class="absolute inset-0 overflow-y-auto"
          (scroll)="onScroll()"
        >
          <div class="mx-auto flex min-h-full w-full max-w-3xl flex-col justify-end gap-1 px-4 py-6 sm:px-6">
            @if (store.messagesLoading()) {
              <div class="flex flex-col gap-6" aria-busy="true">
                @for (i of [1, 2, 3]; track i) {
                  <div class="flex gap-3">
                    <hlm-skeleton class="size-8 shrink-0 rounded-full" />
                    <div class="flex flex-1 flex-col gap-2">
                      <hlm-skeleton class="h-3 w-32" />
                      <hlm-skeleton class="h-4 w-3/4" />
                    </div>
                  </div>
                }
              </div>
            } @else if (store.messagesProblem(); as problem) {
              <app-microsoft-problem [problem]="problem" />
            } @else if (!items().length) {
              <app-empty-state icon="lucideMessagesSquare" title="No messages yet" description="Say hello to start the conversation." />
            } @else {
              @for (item of items(); track item.key) {
                @switch (item.kind) {
                  @case ('day') {
                    <div class="text-muted-foreground my-4 flex justify-center text-xs font-medium">
                      <time [attr.datetime]="item.date">{{ item.label ?? (item.date | date: 'EEE, d MMM') }}</time>
                    </div>
                  }
                  @case ('system') {
                    <p class="text-muted-foreground my-2 text-center text-xs">{{ item.text }}</p>
                  }
                  @case ('msg') {
                    <div
                      class="flex gap-3"
                      [class]="(item.head ? 'mt-4 ' : '') + (item.mine ? 'flex-col items-end' : '')"
                    >
                      @if (item.mine) {
                        <div class="flex max-w-[85%] flex-col items-end gap-1">
                          @if (item.head) {
                            <span class="text-muted-foreground text-xs"><time [attr.datetime]="item.time">{{ item.time | date: 'HH:mm' }}</time></span>
                          }
                          <div class="bg-muted text-foreground min-w-0 rounded-2xl px-4 py-2.5 text-sm" [class.opacity-60]="item.status === 'sending'">
                            <ng-container *ngTemplateOutlet="body; context: { $implicit: item }" />
                          </div>
                          @if (item.status === 'failed') {
                            <div class="text-destructive flex items-center gap-2 text-xs" role="alert">
                              Not sent.
                              <button type="button" class="underline underline-offset-2" (click)="store.retry(item.localId!)">Retry</button>
                              <button type="button" class="underline underline-offset-2" (click)="store.discard(item.localId!)">Discard</button>
                            </div>
                          }
                          <ng-container *ngTemplateOutlet="extras; context: { $implicit: item }" />
                        </div>
                      } @else {
                        <div class="w-8 shrink-0">
                          @if (item.head) {
                            <app-person-avatar [userId]="item.senderId" [name]="item.senderName" />
                          }
                        </div>
                        <div class="min-w-0 flex-1">
                          @if (item.head) {
                            <div class="mb-0.5 flex items-baseline gap-2">
                              <span class="text-sm font-medium">{{ item.senderName }}</span>
                              <span class="text-muted-foreground text-xs"><time [attr.datetime]="item.time">{{ item.time | date: 'HH:mm' }}</time></span>
                            </div>
                          }
                          <div class="min-w-0 text-sm">
                            <ng-container *ngTemplateOutlet="body; context: { $implicit: item }" />
                          </div>
                          <ng-container *ngTemplateOutlet="extras; context: { $implicit: item }" />
                        </div>
                      }
                    </div>
                  }
                }
              }
            }
          </div>
        </div>
        @if (newBelow()) {
          <button
            type="button"
            hlmBtn
            variant="secondary"
            size="sm"
            class="absolute bottom-3 left-1/2 -translate-x-1/2 rounded-full shadow-md"
            (click)="jumpToLatest()"
          >
            <ng-icon name="lucideArrowDown" aria-hidden="true" />
            New messages
          </button>
        }
      </div>

      <form class="mx-auto w-full max-w-3xl px-4 pb-4 sm:px-6" (submit)="submit($event)">
        <div class="bg-background focus-within:border-ring focus-within:ring-ring/30 rounded-3xl border p-3 shadow-xs focus-within:ring-3">
          <label for="teams-composer" class="sr-only">Message {{ chat.title }}</label>
          <textarea
            #composer
            id="teams-composer"
            rows="2"
            class="placeholder:text-muted-foreground field-sizing-content block max-h-36 min-h-12 w-full resize-none bg-transparent px-1 text-sm leading-6 outline-none"
            [placeholder]="'Message ' + chat.title"
            [maxLength]="maxLength"
            [value]="draft()"
            (input)="draft.set($any($event.target).value)"
            (keydown.enter)="onEnter($event)"
          ></textarea>
          <div class="flex items-center justify-end gap-3">
            @if (draft().length > maxLength - 200) {
              <span class="text-muted-foreground text-xs tabular-nums">{{ draft().length }} / {{ maxLength }}</span>
            }
            <button
              hlmBtn
              type="submit"
              size="icon-lg"
              class="size-9 rounded-full"
              [variant]="canSend() ? 'default' : 'secondary'"
              [disabled]="!canSend()"
            >
              @if (sending()) {
                <hlm-spinner aria-label="Sending" />
              } @else {
                <ng-icon name="lucideArrowUp" aria-hidden="true" />
              }
              <span class="sr-only">Send</span>
            </button>
          </div>
        </div>
      </form>
    }

    <ng-template #body let-m>
      @if (m.deleted) {
        <p class="text-muted-foreground italic">This message was deleted</p>
      } @else {
        @if (m.html) {
          <div
            class="leading-relaxed break-words [&_a]:text-primary [&_a]:underline [&_blockquote]:border-border [&_blockquote]:text-muted-foreground [&_blockquote]:border-s-2 [&_blockquote]:ps-3 [&_ol]:list-decimal [&_ol]:ps-5 [&_p+p]:mt-2 [&_pre]:overflow-x-auto [&_strong]:font-semibold [&_ul]:list-disc [&_ul]:ps-5"
            [innerHTML]="m.html"
            (click)="onBodyClick($event)"
          ></div>
        } @else if (m.text) {
          <p class="leading-relaxed break-words whitespace-pre-wrap">{{ m.text }}</p>
        }
      }
    </ng-template>

    <ng-template #extras let-m>
      @if (m.attachments.length) {
        <ul class="mt-1.5 flex flex-wrap gap-2">
          @for (a of m.attachments; track $index) {
            <li class="bg-muted/60 flex max-w-full items-center gap-2 rounded-lg border px-2.5 py-1.5 text-xs">
              <ng-icon name="lucidePaperclip" aria-hidden="true" />
              <span class="truncate">{{ a.name }}</span>
              @if (store.openChat()?.link; as link) {
                <a class="text-primary shrink-0 underline" [href]="link" target="_blank" rel="noopener">
                  Open in Teams<span class="sr-only"> (opens in a new tab)</span>
                </a>
              }
            </li>
          }
        </ul>
      }
      @if (m.reactions.length) {
        <ul class="mt-1.5 flex flex-wrap gap-1.5" aria-label="Reactions">
          @for (r of m.reactions; track r.label) {
            <li class="bg-muted/60 rounded-full border px-2 py-0.5 text-xs">{{ r.label }} {{ r.count }}</li>
          }
        </ul>
      }
    </ng-template>
  `,
  host: { class: 'flex min-h-0 flex-1 flex-col' },
})
export class TeamsConversation {
  protected readonly store = inject(TeamsStore);
  private readonly injector = inject(Injector);

  readonly back = output<void>();

  protected readonly maxLength = MAX_LENGTH;
  protected readonly draft = signal('');
  protected readonly newBelow = signal(false);
  private readonly log = viewChild<ElementRef<HTMLElement>>('log');
  private readonly composer = viewChild<ElementRef<HTMLTextAreaElement>>('composer');

  private atBottom = true;
  private forceScroll = true;
  private lastKey: string | null = null;

  protected readonly sending = computed(() => this.store.pending().some((p) => p.status === 'sending'));
  protected readonly canSend = computed(() => !!this.draft().trim() && !this.sending());

  protected readonly items = computed<LogItem[]>(() => {
    const chatId = this.store.openId();
    const mine = this.store.meId();
    const today = dayKey(new Date());
    const yesterdayDate = new Date();
    yesterdayDate.setDate(yesterdayDate.getDate() - 1);
    const yesterday = dayKey(yesterdayDate);

    const entries = [
      ...this.store.messages().map((m) => ({ message: m, pending: null })),
      ...this.store.pending().filter((p) => p.chatId === chatId).map((p) => ({ message: null, pending: p })),
    ];

    const out: LogItem[] = [];
    let lastDay = '';
    let prev: Extract<LogItem, { kind: 'msg' }> | null = null;
    let prevTime = 0;
    const seen = new Set<string>();

    for (const entry of entries) {
      const m = entry.message;
      const p = entry.pending;
      const id = m?.id ?? p!.localId;
      if (seen.has(id)) continue;
      seen.add(id);
      const time = m?.createdDateTime ?? p!.createdDateTime;
      const date = new Date(time);
      const key = dayKey(date);
      if (key !== lastDay) {
        lastDay = key;
        prev = null;
        out.push({ kind: 'day', key: `day-${key}`, label: key === today ? 'Today' : key === yesterday ? 'Yesterday' : null, date: time });
      }

      if (m && m.messageType && m.messageType !== 'message') {
        prev = null;
        out.push({ kind: 'system', key: id, text: bodyText(m.body) || 'Chat updated' });
        continue;
      }

      const senderId = m ? (m.from?.user?.id ?? null) : mine;
      const isMine = m ? !!mine && senderId === mine : true;
      const rendered = m && !m.deletedDateTime ? renderBody(m.body?.content, m.body?.contentType) : null;
      const attachments: Attachment[] = [
        ...(m?.attachments ?? [])
          .filter((a) => a.contentType !== 'messageReference' && !a.contentType?.startsWith('application/vnd.microsoft.card'))
          .map((a) => ({ name: a.name || 'Attachment' })),
        ...Array.from({ length: rendered?.removedImages ?? 0 }, () => ({ name: 'Image' })),
      ];
      const counts = new Map<string, number>();
      for (const r of m?.reactions ?? []) {
        const label = r.reactionType || 'reaction';
        counts.set(label, (counts.get(label) ?? 0) + 1);
      }
      const senderName = m?.from?.user?.displayName || (isMine ? 'You' : 'Unknown');
      const head = !prev || prev.mine !== isMine || prev.senderId !== senderId || date.getTime() - prevTime > GROUP_WINDOW_MS;
      const item: Extract<LogItem, { kind: 'msg' }> = {
        kind: 'msg',
        key: id,
        localId: p?.localId ?? null,
        mine: isMine,
        senderId,
        senderName,
        head,
        time,
        deleted: !!m?.deletedDateTime,
        html: rendered?.html ?? '',
        text: m ? (rendered && m.body?.contentType === 'html' ? (rendered.html ? '' : rendered.text) : (m.body?.content ?? '')) : p!.text,
        attachments,
        reactions: [...counts].map(([label, count]) => ({ label, count })),
        status: p ? p.status : 'sent',
      };
      out.push(item);
      prev = item;
      prevTime = date.getTime();
    }
    return out;
  });

  constructor() {
    // A different chat starts empty and jumps to its newest message once loaded.
    effect(() => {
      this.store.openId();
      untracked(() => {
        this.draft.set('');
        this.forceScroll = true;
        this.atBottom = true;
        this.lastKey = null;
        this.newBelow.set(false);
        afterNextRender(() => this.composer()?.nativeElement.focus(), { injector: this.injector });
      });
    });

    effect(() => {
      const items = this.items();
      untracked(() => {
        if (!items.length) return;
        const last = items[items.length - 1];
        const changed = last.key !== this.lastKey;
        this.lastKey = last.key;
        const ownLast = last.kind === 'msg' && last.mine;
        if (this.forceScroll || this.atBottom || (changed && ownLast)) {
          this.forceScroll = false;
          afterNextRender(() => this.scrollToEnd(), { injector: this.injector });
        } else if (changed) {
          this.newBelow.set(true);
        }
      });
    });
  }

  private scrollToEnd(): void {
    const el = this.log()?.nativeElement;
    if (el) el.scrollTop = el.scrollHeight;
    this.atBottom = true;
    this.newBelow.set(false);
  }

  protected jumpToLatest(): void {
    this.scrollToEnd();
    this.composer()?.nativeElement.focus();
  }

  protected onScroll(): void {
    const el = this.log()?.nativeElement;
    if (!el) return;
    this.atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < NEAR_BOTTOM_PX;
    if (this.atBottom) this.newBelow.set(false);
  }

  protected onEnter(event: Event): void {
    const e = event as KeyboardEvent;
    if (e.shiftKey || e.isComposing) return;
    e.preventDefault();
    this.submit();
  }

  protected submit(event?: Event): void {
    event?.preventDefault();
    if (!this.canSend()) return;
    this.store.send(this.draft());
    this.draft.set('');
    this.atBottom = true;
  }

  /** Message links open in a new tab even if the sanitizer dropped the target attribute. */
  protected onBodyClick(event: Event): void {
    const anchor = (event.target as HTMLElement | null)?.closest('a');
    const href = anchor?.getAttribute('href');
    if (!anchor || !href) return;
    event.preventDefault();
    if (/^https?:/i.test(href)) window.open(href, '_blank', 'noopener,noreferrer');
  }
}
