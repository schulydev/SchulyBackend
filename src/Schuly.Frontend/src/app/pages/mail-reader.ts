import { DatePipe } from '@angular/common';
import { Component, DestroyRef, ElementRef, Injector, afterNextRender, computed, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideChevronDown,
  lucideDownload,
  lucideEye,
  lucideExternalLink,
  lucideFlag,
  lucideFolder,
  lucideFolderInput,
  lucideForward,
  lucideMail,
  lucideMailOpen,
  lucidePaperclip,
  lucideReply,
  lucideReplyAll,
  lucideTrash2,
} from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';
import { HlmSkeletonImports } from '@spartan-ng/helm/skeleton';
import { initials } from '../core/initials';
import { FileViewer, ViewerFile } from '../shared/file-viewer';
import { MailBody } from '../shared/mail-body';
import { ActionResult, MailAttachment, MailClient } from '../stores/mail-client';
import { MailStore } from '../stores/microsoft.store';
import { ComposeMode } from './mail-compose';

export interface MailSummary {
  id: string;
  sender: string;
  address: string | null;
  subject: string;
  preview: string;
  to: string[];
  received: string | null;
  unread: boolean;
  flagged: boolean;
  link: string | null;
}

const formatSize = (bytes: number) => (bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`);

/** The reading pane: toolbar, header, attachments and the sandboxed body. Falls back to the list preview when the server cannot return full messages. */
@Component({
  selector: 'app-mail-reader',
  imports: [DatePipe, HlmButton, HlmDropdownMenuImports, HlmSkeletonImports, NgIcon, MailBody, FileViewer],
  providers: [
    provideIcons({
      lucideArrowLeft,
      lucideChevronDown,
      lucideDownload,
      lucideEye,
      lucideExternalLink,
      lucideFlag,
      lucideFolder,
      lucideFolderInput,
      lucideForward,
      lucideMail,
      lucideMailOpen,
      lucidePaperclip,
      lucideReply,
      lucideReplyAll,
      lucideTrash2,
    }),
  ],
  template: `
    <div role="toolbar" aria-label="Message actions" class="flex min-h-14 shrink-0 flex-wrap items-center gap-1 border-b px-4 py-1 lg:px-6">
      <button hlmBtn variant="ghost" size="sm" type="button" class="lg:hidden" (click)="back.emit()">
        <ng-icon name="lucideArrowLeft" aria-hidden="true" />
        <span class="sr-only">Back to list</span>
      </button>
      <button hlmBtn variant="ghost" size="sm" type="button" [disabled]="!detail()" (click)="compose.emit('reply')">
        <ng-icon name="lucideReply" aria-hidden="true" />
        <span class="@max-3xl:sr-only">Reply</span>
      </button>
      <button hlmBtn variant="ghost" size="sm" type="button" [disabled]="!detail()" (click)="compose.emit('replyAll')">
        <ng-icon name="lucideReplyAll" aria-hidden="true" />
        <span class="@max-3xl:sr-only">Reply all</span>
      </button>
      <button hlmBtn variant="ghost" size="sm" type="button" [disabled]="!detail()" (click)="compose.emit('forward')">
        <ng-icon name="lucideForward" aria-hidden="true" />
        <span class="@max-3xl:sr-only">Forward</span>
      </button>
      <button hlmBtn variant="ghost" size="sm" type="button" (click)="remove()">
        <ng-icon name="lucideTrash2" aria-hidden="true" />
        <span class="@max-3xl:sr-only">Delete</span>
      </button>
      <button hlmBtn variant="ghost" size="sm" type="button" [attr.aria-pressed]="flagged()" (click)="toggleFlag()">
        <ng-icon name="lucideFlag" [class.text-destructive]="flagged()" aria-hidden="true" />
        <span class="@max-3xl:sr-only">{{ flagged() ? 'Unflag' : 'Flag' }}</span>
      </button>
      <button hlmBtn variant="ghost" size="sm" type="button" (click)="toggleRead()">
        <ng-icon [name]="read() ? 'lucideMail' : 'lucideMailOpen'" aria-hidden="true" />
        <span class="@max-3xl:sr-only">{{ read() ? 'Mark as unread' : 'Mark as read' }}</span>
      </button>
      <div class="ms-auto flex items-center gap-2">
        <button hlmBtn variant="ghost" size="sm" type="button" [hlmDropdownMenuTrigger]="moveMenu" align="end">
          <ng-icon name="lucideFolderInput" aria-hidden="true" />
          <span class="@max-3xl:sr-only">Move to</span>
          <ng-icon name="lucideChevronDown" class="@max-3xl:hidden" aria-hidden="true" />
        </button>
      </div>
    </div>
    <ng-template #moveMenu>
      <hlm-dropdown-menu class="max-h-80 w-64" aria-label="Move to folder">
        <hlm-dropdown-menu-label>Move to folder</hlm-dropdown-menu-label>
        <hlm-dropdown-menu-separator />
        @for (folder of targets(); track folder.key) {
          <button hlmDropdownMenuItem type="button" [style.padding-inline-start.rem]="0.5 + folder.depth * 1.25" (click)="move(folder.key)">
            <ng-icon name="lucideFolder" aria-hidden="true" />
            <span class="min-w-0 flex-1 truncate text-start">{{ folder.name }}</span>
          </button>
          @if (folder.group === 'main' && isLastMain(folder.key) && moreTargets()) {
            <hlm-dropdown-menu-separator />
          }
        }
      </hlm-dropdown-menu>
    </ng-template>
    <p class="text-muted-foreground px-4 text-xs empty:hidden" role="status" aria-live="polite">{{ notice() }}</p>

    <div class="flex min-h-0 min-w-0 flex-1 flex-col gap-3 overflow-y-auto overflow-x-hidden p-4 lg:p-6" [attr.aria-busy]="client.detailLoading()">
      <h3 #title tabindex="-1" class="text-2xl leading-tight font-semibold tracking-tight wrap-anywhere outline-none">
        {{ detail()?.subject ?? summary().subject }}
      </h3>
      <div class="flex flex-wrap items-start gap-x-3 gap-y-1">
        <span class="bg-muted flex size-10 shrink-0 items-center justify-center rounded-full text-sm font-medium" aria-hidden="true">{{ letters() }}</span>
        <div class="min-w-0 flex-1 text-sm">
          <p class="truncate font-semibold">{{ detail()?.from?.name ?? summary().sender }}</p>
          @if ((detail()?.from?.address ?? summary().address); as address) {
            @if (address !== (detail()?.from?.name ?? summary().sender)) {
              <p class="text-muted-foreground truncate">{{ address }}</p>
            }
          }
          @if (recipients(); as to) {
            <p class="text-muted-foreground truncate">To: {{ to }}</p>
          }
          @if (cc(); as cc) {
            <p class="text-muted-foreground truncate">Cc: {{ cc }}</p>
          }
        </div>
        @if (detail()?.received ?? summary().received; as received) {
          <time class="text-muted-foreground shrink-0 text-xs max-sm:ps-13" [attr.datetime]="received">{{ received | date: 'EEE d MMM y, HH:mm' }}</time>
        }
      </div>

      @if (files().length) {
        <ul class="flex flex-col gap-2 border-y py-3" aria-label="Attachments">
          @for (file of files(); track file.id) {
            <li class="flex min-w-0 flex-col gap-2">
              <div class="flex min-w-0 flex-wrap items-center gap-x-2 text-sm">
                <ng-icon name="lucidePaperclip" class="shrink-0" aria-hidden="true" />
                <span class="min-w-24 flex-1 truncate">{{ file.name }}</span>
                <span class="text-muted-foreground shrink-0 text-xs">{{ size(file.size) }}</span>
                <button hlmBtn type="button" variant="ghost" size="sm" class="ms-auto shrink-0" (click)="download(file)">
                  <ng-icon name="lucideDownload" aria-hidden="true" />
                  Download<span class="sr-only"> {{ file.name }}</span>
                </button>
                @if (client.isPreviewable(file)) {
                  <button hlmBtn type="button" variant="ghost" size="sm" class="shrink-0" (click)="preview(file)">
                    <ng-icon name="lucideEye" aria-hidden="true" />
                    View<span class="sr-only"> {{ file.name }}</span>
                  </button>
                }
              </div>
            </li>
          }
        </ul>
      }

      @if (detail(); as d) {
        <app-mail-body class="min-h-96" [body]="d.body" [html]="d.html" />
      } @else if (client.detailLoading()) {
        <div class="flex flex-col gap-2" aria-hidden="true">
          <hlm-skeleton class="h-4 w-full" />
          <hlm-skeleton class="h-4 w-11/12" />
          <hlm-skeleton class="h-4 w-4/5" />
        </div>
      } @else {
        <div class="flex flex-col gap-3">
          <p class="text-sm leading-relaxed wrap-anywhere whitespace-pre-line">{{ summary().preview || 'No preview available.' }}</p>
          <div class="flex flex-wrap items-center gap-3 border-t pt-3">
            @if (summary().link; as link) {
              <a hlmBtn size="sm" [href]="link" target="_blank" rel="noopener">
                Open in Outlook
                <ng-icon name="lucideExternalLink" aria-hidden="true" />
                <span class="sr-only">(opens in a new tab)</span>
              </a>
            }
            <p class="text-muted-foreground text-xs">
              {{ client.detailProblem() === 'unavailable' ? 'This server cannot show full messages yet. Showing a preview.' : 'The full message could not be loaded. Showing a preview.' }}
            </p>
          </div>
        </div>
      }
    </div>
    <app-file-viewer [file]="viewing()" (closed)="viewing.set(null)" />
  `,
  host: { class: '@container flex min-h-0 min-w-0 flex-1 flex-col' },
})
export class MailReader {
  protected readonly client = inject(MailClient);
  private readonly mail = inject(MailStore);
  private readonly injector = inject(Injector);

  readonly summary = input.required<MailSummary>();
  readonly back = output<void>();
  readonly compose = output<ComposeMode>();
  /** The message left this folder (deleted or moved). */
  readonly removed = output<void>();

  private readonly title = viewChild<ElementRef<HTMLElement>>('title');
  protected readonly notice = signal('');
  protected readonly viewing = signal<ViewerFile | null>(null);

  /** The effect below must only rerun when the message changes, not when the list patches its summary. */
  private readonly id = computed(() => this.summary().id);
  protected readonly detail = this.client.detail;
  protected readonly size = formatSize;
  protected readonly letters = computed(() => initials(this.detail()?.from?.name ?? this.summary().sender) || '?');
  protected readonly recipients = computed(() => (this.detail()?.to.map((p) => p.name) ?? this.summary().to).join(', '));
  protected readonly cc = computed(() => (this.detail()?.cc.map((p) => p.name) ?? []).join(', '));
  protected readonly flagged = computed(() => this.detail()?.flagged ?? this.summary().flagged);
  protected readonly read = computed(() => this.detail()?.read ?? !this.summary().unread);
  protected readonly files = computed(() => this.client.attachments().filter((a) => !a.inline));
  protected readonly targets = computed(() => this.client.folders().filter((f) => f.key !== this.mail.folder()));
  private readonly lastMain = computed(() => this.targets().filter((f) => f.group === 'main').at(-1)?.key ?? null);
  protected readonly moreTargets = computed(() => this.targets().some((f) => f.group === 'more'));
  protected readonly isLastMain = (key: string) => key === this.lastMain();

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => {
        this.viewing.set(null);
        this.notice.set('');
        void this.client.open(id).then(() => {
          if (this.summary().id === id && this.summary().unread) void this.markRead(id);
        });
        afterNextRender(() => this.title()?.nativeElement.focus(), { injector: this.injector });
      });
    });
    inject(DestroyRef).onDestroy(() => {
      this.client.close();
    });
  }

  private say(result: ActionResult, success: string): boolean {
    this.notice.set(result === 'ok' ? success : result === 'unavailable' ? 'Not available on this server yet.' : 'That did not work. Try again.');
    return result === 'ok';
  }

  private async markRead(id: string): Promise<void> {
    if ((await this.client.setRead(id, true)) === 'ok') this.mail.patch(id, { isRead: true });
  }

  protected async toggleRead(): Promise<void> {
    const id = this.summary().id;
    const next = !this.read();
    if (this.say(await this.client.setRead(id, next), next ? 'Marked as read' : 'Marked as unread')) this.mail.patch(id, { isRead: next });
  }

  protected async toggleFlag(): Promise<void> {
    const id = this.summary().id;
    const next = !this.flagged();
    if (this.say(await this.client.setFlag(id, next), next ? 'Flagged' : 'Flag removed')) {
      this.mail.patch(id, { flag: { flagStatus: next ? 'flagged' : 'notFlagged' } });
    }
  }

  protected async remove(): Promise<void> {
    const id = this.summary().id;
    if (this.say(await this.client.remove(id), 'Message deleted')) {
      this.mail.drop(id);
      this.removed.emit();
    }
  }

  protected async move(folder: string | null | undefined): Promise<void> {
    if (!folder) return;
    const id = this.summary().id;
    if (this.say(await this.client.move(id, folder), 'Message moved')) {
      this.mail.drop(id);
      this.removed.emit();
    }
  }

  protected async download(file: MailAttachment): Promise<void> {
    const ok = await this.client.download(this.summary().id, file);
    this.notice.set(ok ? `Downloaded ${file.name}` : `Could not download ${file.name}.`);
  }

  protected preview(file: MailAttachment): void {
    const id = this.summary().id;
    this.viewing.set({ name: file.name, load: () => this.client.fetchAttachment(id, file) });
  }
}
