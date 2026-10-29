import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, effect, inject, signal, untracked } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MicrosoftAccountsStore } from '../stores/microsoft-accounts.store';
import { Importance } from './chat-media';
import { createQueue } from './request-queue';
import { Catalogue, catalogueLocale, parseCatalogue } from './teams-emoji';

export interface Gif {
  id: string;
  title: string;
  url: string;
  previewUrl: string;
  width: number;
  height: number;
}

export interface ScheduledMessage {
  id: string;
  chatId: string;
  sendAt: string;
  content: string;
  contentType: string;
  importance?: Importance | null;
}

export interface MeetingResult {
  id?: string;
  subject?: string;
  start?: string;
  end?: string;
  webLink?: string | null;
  joinUrl?: string | null;
}

export type GifResult = { status: 'ok'; items: Gif[] } | { status: 'unavailable' } | { status: 'error' };

const MAX_CONCURRENT_MEDIA = 4;

/**
 * Turns a path into an authenticated request URL under this account's API, or null when it is not safe to send the session to.
 * Relative paths are built by the helpers above; absolute ones must already be under `base`. Never accepted: protocol-relative
 * (`//host`), a scheme, backslashes, control characters, and `.` or `..` segments.
 */
export function safeApiPath(path: string, base: string): string | null {
  // eslint-disable-next-line no-control-regex
  if (!path || /[\\\u0000-\u001f]/.test(path) || path.startsWith('//')) return null;
  const [pathname] = path.split(/[?#]/, 1);
  const segments = pathname.split('/');
  for (const segment of segments) {
    let decoded: string;
    try {
      decoded = decodeURIComponent(segment);
    } catch {
      return null;
    }
    if (decoded === '.' || decoded === '..') return null;
  }
  if (path.startsWith('/')) return path.startsWith(`${base}/`) ? path : null;
  if (/^[a-z][a-z0-9+.-]*:/i.test(path)) return null;
  return `${base}/${path}`;
}

/** Everything the Teams composer and message bubbles ask of accounts/{id}/... besides the chat list and messages themselves. */
@Injectable()
export class TeamsApi {
  private readonly http = inject(HttpClient);
  private readonly accounts = inject(MicrosoftAccountsStore);

  /** null until the GIF endpoint answered once: true when configured, false on 503. */
  readonly gifsAvailable = signal<boolean | null>(null);

  private readonly queue = createQueue(MAX_CONCURRENT_MEDIA);
  private readonly urls = new Map<string, Promise<string | null>>();
  private catalogueCache: { account: string; promise: Promise<Catalogue | null> } | null = null;

  constructor() {
    // A different account must not see another account's cached blobs.
    effect(() => {
      this.accounts.selectedId();
      untracked(() => {
        this.revokeAll();
        this.gifsAvailable.set(null);
        this.catalogueCache = null;
      });
    });
  }

  base(): string | null {
    const id = this.accounts.selectedId();
    return id ? `/api/plugins/microsoft/accounts/${encodeURIComponent(id)}` : null;
  }

  private require(): string {
    const base = this.base();
    if (!base) throw new Error('No Microsoft account selected.');
    return base;
  }

  chatPath(chatId: string): string {
    return `chats/${encodeURIComponent(chatId)}`;
  }

  mediaPath(chatId: string, messageId: string, hostedId: string): string {
    return `${this.chatPath(chatId)}/messages/${encodeURIComponent(messageId)}/media/${encodeURIComponent(hostedId)}`;
  }

  filePath(chatId: string, messageId: string, fileId: string): string {
    return `${this.chatPath(chatId)}/messages/${encodeURIComponent(messageId)}/files/${encodeURIComponent(fileId)}`;
  }

  /** The bytes behind an API path (or an absolute `/api/...` path), fetched with the signed-in session. */
  blob(path: string): Promise<Blob> {
    const url = safeApiPath(path, this.require());
    if (!url) return Promise.reject(new Error('Refusing to fetch an unsafe path.'));
    return firstValueFrom(this.http.get(url, { responseType: 'blob' }));
  }

  /** An object URL for an image behind an API path, de-duplicated and fetched at most four at a time; null when it has none. */
  imageUrl(path: string): Promise<string | null> {
    const key = `${this.accounts.selectedId()}|${path}`;
    let found = this.urls.get(key);
    if (!found) {
      found = this.queue(() => this.blob(path))
        .then((blob) => (blob.size > 0 ? URL.createObjectURL(blob) : null))
        .catch(() => null);
      this.urls.set(key, found);
    }
    return found;
  }

  private revokeAll(): void {
    for (const pending of this.urls.values()) void pending.then((url) => url && URL.revokeObjectURL(url));
    this.urls.clear();
  }

  async gifs(query: string | null, limit = 24): Promise<GifResult> {
    const base = this.base();
    if (!base) return { status: 'error' };
    try {
      const url = query ? `${base}/gifs/search` : `${base}/gifs/trending`;
      const params: Record<string, string | number> = query ? { q: query, limit } : { limit };
      const items = await firstValueFrom(this.http.get<Gif[]>(url, { params }));
      this.gifsAvailable.set(true);
      return { status: 'ok', items: Array.isArray(items) ? items : [] };
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 503) {
        this.gifsAvailable.set(false);
        return { status: 'unavailable' };
      }
      return { status: 'error' };
    }
  }

  /**
   * Teams' own emoji catalogue (large, so it is fetched the first time something needs it and then kept in memory);
   * null when the server has none, so callers fall back to unicode.
   */
  catalogue(): Promise<Catalogue | null> {
    const account = this.accounts.selectedId();
    const base = this.base();
    if (!account || !base) return Promise.resolve(null);
    if (this.catalogueCache?.account !== account) {
      const locale = catalogueLocale(typeof navigator === 'undefined' ? undefined : navigator.language);
      this.catalogueCache = {
        account,
        promise: firstValueFrom(this.http.get<unknown>(`${base}/emojis/catalogue`, { params: { locale } })).then(parseCatalogue, () => null),
      };
    }
    return this.catalogueCache.promise;
  }

  async scheduled(chatId: string): Promise<ScheduledMessage[]> {
    const items = await firstValueFrom(this.http.get<ScheduledMessage[]>(`${this.require()}/scheduled`));
    return (Array.isArray(items) ? items : []).filter((m) => m.chatId === chatId);
  }

  schedule(chatId: string, body: { content: string; contentType: 'text' | 'html'; importance?: Importance; sendAt: string }): Promise<ScheduledMessage> {
    return firstValueFrom(this.http.post<ScheduledMessage>(`${this.require()}/${this.chatPath(chatId)}/scheduled`, body));
  }

  async cancelScheduled(id: string): Promise<void> {
    await firstValueFrom(this.http.delete(`${this.require()}/scheduled/${encodeURIComponent(id)}`));
  }

  meeting(chatId: string, body: { subject: string; start: string; end: string; description?: string }): Promise<MeetingResult> {
    return firstValueFrom(this.http.post<MeetingResult>(`${this.require()}/${this.chatPath(chatId)}/meeting`, body));
  }
}

/** A readable reason for a failed request, from the plugin's `{message}` body when it has one. */
export function apiMessage(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { message?: unknown } | null;
    if (body && typeof body.message === 'string' && body.message) return body.message;
  }
  return fallback;
}
