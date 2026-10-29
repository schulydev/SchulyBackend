import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { swrCache } from '../core/swr-cache';
import { MicrosoftAccountsStore } from './microsoft-accounts.store';

export type FolderKind = 'inbox' | 'sent' | 'drafts' | 'archive' | 'deleted' | 'junk' | 'outbox' | 'other';

export interface MailFolder {
  /** Value for the list's `folder` parameter and for move: a well-known name when there is one, else the folder id. */
  key: string;
  name: string;
  kind: FolderKind;
  unread: number | null;
}

interface GraphFolder {
  id?: string | null;
  displayName?: string | null;
  wellKnownName?: string | null;
  unreadItemCount?: number | null;
  childFolders?: GraphFolder[] | null;
}

interface GraphAddress {
  emailAddress?: { name?: string | null; address?: string | null } | null;
}

interface GraphMessage {
  id: string;
  subject?: string | null;
  receivedDateTime?: string | null;
  isRead?: boolean | null;
  webLink?: string | null;
  hasAttachments?: boolean | null;
  flag?: { flagStatus?: string | null } | null;
  from?: GraphAddress | null;
  toRecipients?: GraphAddress[] | null;
  ccRecipients?: GraphAddress[] | null;
  body?: { contentType?: string | null; content?: string | null } | null;
}

export interface MailPerson {
  name: string;
  address: string;
}

export interface MailDetail {
  id: string;
  subject: string;
  from: MailPerson | null;
  to: MailPerson[];
  cc: MailPerson[];
  received: string | null;
  html: boolean;
  body: string;
  hasAttachments: boolean;
  flagged: boolean;
  read: boolean;
  link: string | null;
}

export interface MailAttachment {
  id: string;
  name: string;
  contentType: string;
  size: number;
  inline: boolean;
}

interface GraphAttachment {
  id?: string | null;
  name?: string | null;
  contentType?: string | null;
  size?: number | null;
  isInline?: boolean | null;
}

export interface OutgoingMail {
  to: string[];
  cc: string[];
  bcc: string[];
  subject: string;
  body: string;
  attachments: { name: string; contentType: string; contentBase64: string }[];
}

/** `unavailable` means the server has no such endpoint yet (404/405/501), so the UI can degrade instead of failing. */
export type ActionResult = 'ok' | 'unavailable' | 'failed';

/** Largest total attachment size the server accepts. */
export const MAX_ATTACHMENT_BYTES = 3 * 1024 * 1024;

/** Only these may ever be shown inline, as an img from a blob URL; everything else is download-only. */
const PREVIEWABLE_IMAGES = new Set(['image/png', 'image/jpeg', 'image/gif', 'image/webp']);

const WELL_KNOWN: Record<string, FolderKind> = {
  inbox: 'inbox',
  sentitems: 'sent',
  drafts: 'drafts',
  archive: 'archive',
  deleteditems: 'deleted',
  junkemail: 'junk',
  outbox: 'outbox',
};

/** Shown when the server has no folder endpoint yet. */
export const FALLBACK_FOLDERS: MailFolder[] = [
  { key: 'inbox', name: 'Inbox', kind: 'inbox', unread: null },
  { key: 'sentitems', name: 'Sent Items', kind: 'sent', unread: null },
  { key: 'drafts', name: 'Drafts', kind: 'drafts', unread: null },
  { key: 'archive', name: 'Archive', kind: 'archive', unread: null },
  { key: 'deleteditems', name: 'Deleted Items', kind: 'deleted', unread: null },
];

const person = (a?: GraphAddress | null): MailPerson | null => {
  const address = a?.emailAddress?.address ?? '';
  const name = a?.emailAddress?.name || address;
  return name || address ? { name, address } : null;
};

const people = (list?: GraphAddress[] | null): MailPerson[] => (list ?? []).map(person).filter((p): p is MailPerson => !!p);

const statusOf = (error: unknown): ActionResult =>
  error instanceof HttpErrorResponse && [404, 405, 501].includes(error.status) ? 'unavailable' : 'failed';

/** File name that is safe to hand to a download link: no path parts, control characters or leading dots, bounded length. */
export function safeFileName(name: string): string {
  const cleaned = name
    // eslint-disable-next-line no-control-regex
    .replace(/[\u0000-\u001f\u007f]/g, '')
    .replace(/[\\/:*?"<>|]+/g, '_')
    .replace(/^[._\s]+/, '')
    .trim()
    .slice(0, 120);
  return cleaned || 'attachment';
}

/** Everything the Mail page does against accounts/{id}/mail/...; cached per account so folders and messages show at once. */
@Injectable()
export class MailClient {
  private readonly http = inject(HttpClient);
  private readonly accounts = inject(MicrosoftAccountsStore);

  readonly folders = signal<MailFolder[]>(FALLBACK_FOLDERS);
  /** False while the server only offers the built-in folder list. */
  readonly foldersLive = signal(false);

  readonly detail = signal<MailDetail | null>(null);
  readonly detailLoading = signal(false);
  readonly detailProblem = signal<'unavailable' | 'failed' | null>(null);
  readonly attachments = signal<MailAttachment[]>([]);

  readonly unreadTotal = computed(() => this.folders().find((f) => f.kind === 'inbox')?.unread ?? null);

  private openSeq = 0;

  private base(): string | null {
    const id = this.accounts.selectedId();
    return id ? `/api/plugins/microsoft/accounts/${encodeURIComponent(id)}/mail` : null;
  }

  private key(suffix: string): string {
    return `${this.accounts.selectedId()}|mail|${suffix}`;
  }

  async loadFolders(): Promise<void> {
    const base = this.base();
    if (!base) return;
    const key = this.key('folders');
    const hit = swrCache.get<MailFolder[]>(key);
    if (hit) {
      this.folders.set(hit.data);
      this.foldersLive.set(true);
    }
    try {
      const items = await firstValueFrom(this.http.get<GraphFolder[]>(`${base}/folders`));
      if (!Array.isArray(items) || !items.length) throw new Error('no folders');
      const flat = items.flatMap((f) => [f, ...(f.childFolders ?? [])]);
      const folders = flat
        .filter((f) => f.id && f.displayName)
        .map((f): MailFolder => {
          const wellKnown = (f.wellKnownName || f.displayName!.replace(/\s+/g, '')).toLowerCase();
          const kind = WELL_KNOWN[wellKnown] ?? 'other';
          return { key: kind === 'other' ? f.id! : wellKnown, name: f.displayName!, kind, unread: f.unreadItemCount ?? null };
        });
      swrCache.set(key, folders);
      this.folders.set(folders);
      this.foldersLive.set(true);
    } catch {
      // Without the endpoint (older server) the built-in list stays.
      if (!hit) {
        this.folders.set(FALLBACK_FOLDERS);
        this.foldersLive.set(false);
      }
    }
  }

  private parse(m: GraphMessage): MailDetail {
    return {
      id: m.id,
      subject: m.subject || '(no subject)',
      from: person(m.from),
      to: people(m.toRecipients),
      cc: people(m.ccRecipients),
      received: m.receivedDateTime ?? null,
      html: (m.body?.contentType ?? '').toLowerCase() === 'html',
      body: m.body?.content ?? '',
      hasAttachments: m.hasAttachments === true,
      flagged: m.flag?.flagStatus === 'flagged',
      read: m.isRead !== false,
      link: m.webLink ?? null,
    };
  }

  /** Shows the cached message at once (if any) and refreshes it. */
  async open(id: string): Promise<void> {
    const base = this.base();
    if (!base) return;
    const seq = ++this.openSeq;
    const key = this.key(`msg|${id}`);
    const hit = swrCache.get<MailDetail>(key);
    this.detailProblem.set(null);
    this.attachments.set([]);
    if (hit) {
      this.detail.set(hit.data);
      this.detailLoading.set(false);
      if (hit.data.hasAttachments) void this.loadAttachments(id, seq);
    } else {
      this.detail.set(null);
      this.detailLoading.set(true);
    }
    try {
      const message = await firstValueFrom(this.http.get<GraphMessage>(`${base}/${encodeURIComponent(id)}`));
      if (seq !== this.openSeq) return;
      const detail = this.parse(message);
      swrCache.set(key, detail);
      this.detail.set(detail);
      this.detailLoading.set(false);
      if (detail.hasAttachments && !hit?.data.hasAttachments) void this.loadAttachments(id, seq);
    } catch (error) {
      if (seq !== this.openSeq) return;
      this.detailLoading.set(false);
      if (!hit) this.detailProblem.set(statusOf(error) === 'unavailable' ? 'unavailable' : 'failed');
    }
  }

  close(): void {
    this.openSeq++;
    this.detail.set(null);
    this.detailLoading.set(false);
    this.detailProblem.set(null);
    this.attachments.set([]);
  }

  /** Warms the cache for a message the user is about to open (hover or focus). */
  prefetch(id: string): void {
    const base = this.base();
    if (!base) return;
    const key = this.key(`msg|${id}`);
    const hit = swrCache.get(key);
    if (hit && hit.age < 60_000) return;
    this.http.get<GraphMessage>(`${base}/${encodeURIComponent(id)}`).subscribe({
      next: (m) => swrCache.set(key, this.parse(m)),
      error: () => undefined,
    });
  }

  private async loadAttachments(id: string, seq: number): Promise<void> {
    const base = this.base();
    if (!base) return;
    try {
      const items = await firstValueFrom(this.http.get<GraphAttachment[]>(`${base}/${encodeURIComponent(id)}/attachments`));
      if (seq !== this.openSeq || !Array.isArray(items)) return;
      this.attachments.set(
        items
          .filter((a) => a.id)
          .map((a) => ({
            id: a.id!,
            name: safeFileName(a.name ?? ''),
            contentType: (a.contentType ?? '').toLowerCase(),
            size: a.size ?? 0,
            inline: a.isInline === true,
          })),
      );
    } catch {
      // The message still reads fine without its attachment list.
    }
  }

  isPreviewable(attachment: MailAttachment): boolean {
    return PREVIEWABLE_IMAGES.has(attachment.contentType);
  }

  private fetchAttachment(messageId: string, attachment: MailAttachment): Promise<Blob> {
    return firstValueFrom(
      this.http.get(`${this.base()}/${encodeURIComponent(messageId)}/attachments/${encodeURIComponent(attachment.id)}`, { responseType: 'blob' }),
    );
  }

  /**
   * Fetches the attachment with the signed-in session and saves it through a temporary download link. The URL is never navigated to,
   * opened in a tab or framed, and the blob is typed as opaque bytes so the browser can never render it.
   */
  async download(messageId: string, attachment: MailAttachment): Promise<boolean> {
    try {
      const blob = new Blob([await this.fetchAttachment(messageId, attachment)], { type: 'application/octet-stream' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = safeFileName(attachment.name);
      link.rel = 'noopener';
      link.hidden = true;
      document.body.append(link);
      link.click();
      link.remove();
      setTimeout(() => URL.revokeObjectURL(url), 0);
      return true;
    } catch {
      return false;
    }
  }

  /** Blob URL for an img preview of a png, jpeg, gif or webp attachment (by declared MIME type); null for anything else. The caller revokes it. */
  async previewImage(messageId: string, attachment: MailAttachment): Promise<string | null> {
    if (!this.isPreviewable(attachment)) return null;
    try {
      const blob = new Blob([await this.fetchAttachment(messageId, attachment)], { type: attachment.contentType });
      return URL.createObjectURL(blob);
    } catch {
      return null;
    }
  }

  private async post(path: string, body: unknown): Promise<ActionResult> {
    const base = this.base();
    if (!base) return 'failed';
    try {
      await firstValueFrom(this.http.post(`${base}/${path}`, body));
      return 'ok';
    } catch (error) {
      return statusOf(error);
    }
  }

  private forget(id: string): void {
    swrCache.invalidate(this.key(`msg|${id}`));
  }

  /** Lists are cached per folder; after a move, delete or send they must be refetched. */
  private forgetLists(): void {
    swrCache.invalidate(this.key('['));
  }

  async setRead(id: string, read: boolean): Promise<ActionResult> {
    const result = await this.post(`${encodeURIComponent(id)}/read`, { read });
    if (result === 'ok') {
      this.detail.update((d) => (d && d.id === id ? { ...d, read } : d));
      this.forget(id);
      this.forgetLists();
      void this.loadFolders();
    }
    return result;
  }

  async setFlag(id: string, flagged: boolean): Promise<ActionResult> {
    const result = await this.post(`${encodeURIComponent(id)}/flag`, { status: flagged ? 'flagged' : 'notFlagged' });
    if (result === 'ok') {
      this.detail.update((d) => (d && d.id === id ? { ...d, flagged } : d));
      this.forget(id);
      this.forgetLists();
    }
    return result;
  }

  async move(id: string, folder: string): Promise<ActionResult> {
    const result = await this.post(`${encodeURIComponent(id)}/move`, { folder });
    if (result === 'ok') {
      this.forget(id);
      this.forgetLists();
      void this.loadFolders();
    }
    return result;
  }

  async remove(id: string): Promise<ActionResult> {
    const base = this.base();
    if (!base) return 'failed';
    try {
      await firstValueFrom(this.http.delete(`${base}/${encodeURIComponent(id)}`));
      this.forget(id);
      this.forgetLists();
      void this.loadFolders();
      return 'ok';
    } catch (error) {
      return statusOf(error);
    }
  }

  reply(id: string, comment: string, replyAll: boolean): Promise<ActionResult> {
    return this.post(`${encodeURIComponent(id)}/reply`, { comment, replyAll });
  }

  forward(id: string, to: string[], comment: string): Promise<ActionResult> {
    return this.post(`${encodeURIComponent(id)}/forward`, { to, comment });
  }

  async send(mail: OutgoingMail): Promise<ActionResult> {
    const result = await this.post('send', { ...mail, html: false });
    if (result === 'ok') this.forgetLists();
    return result;
  }

  async saveDraft(mail: OutgoingMail): Promise<ActionResult> {
    const result = await this.post('drafts', { ...mail, html: false });
    if (result === 'ok') this.forgetLists();
    return result;
  }
}
