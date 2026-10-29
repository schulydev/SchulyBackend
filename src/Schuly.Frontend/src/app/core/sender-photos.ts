import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, WritableSignal, effect, inject, signal, untracked } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MicrosoftAccountsStore } from '../stores/microsoft-accounts.store';
import { createQueue } from './request-queue';

export const MISS_TTL_MS = 10 * 60 * 1000;
export const MAX_CONCURRENT_PHOTOS = 4;

/**
 * Profile photos of mail senders by address (`people/photo?email=`). One signal per email, at most four requests at a time,
 * and a miss (404 for external people, or any failure) is remembered in memory for ten minutes.
 */
@Injectable()
export class SenderPhotos {
  private readonly http = inject(HttpClient);
  private readonly accounts = inject(MicrosoftAccountsStore);

  private readonly queue = createQueue(MAX_CONCURRENT_PHOTOS);
  private readonly photos = new Map<string, WritableSignal<string | null>>();
  private readonly misses = new Map<string, number>();
  private readonly inflight = new Set<string>();
  /** Replaceable clock, for tests. */
  now: () => number = () => Date.now();

  constructor() {
    effect(() => {
      this.accounts.selectedId();
      untracked(() => this.clear());
    });
  }

  private clear(): void {
    for (const s of this.photos.values()) {
      const url = s();
      if (url) URL.revokeObjectURL(url);
    }
    this.photos.clear();
    this.misses.clear();
    this.inflight.clear();
  }

  /** The photo signal for an address; the first call (and the first after a cached miss expires) starts the request. */
  photo(email: string | null | undefined): Signal<string | null> {
    const key = (email ?? '').trim().toLowerCase();
    let sig = this.photos.get(key);
    if (!sig) {
      sig = signal<string | null>(null);
      this.photos.set(key, sig);
    }
    if (key.includes('@')) this.load(key, sig);
    return sig;
  }

  private load(key: string, sig: WritableSignal<string | null>): void {
    const accountId = this.accounts.selectedId();
    if (!accountId || sig() || this.inflight.has(key)) return;
    const missedAt = this.misses.get(key);
    if (missedAt !== undefined && this.now() - missedAt < MISS_TTL_MS) return;
    this.inflight.add(key);
    const url = `/api/plugins/microsoft/accounts/${encodeURIComponent(accountId)}/people/photo`;
    void this.queue(() => firstValueFrom(this.http.get(url, { params: { email: key, size: 48 }, responseType: 'blob' })))
      .then((blob) => {
        if (this.accounts.selectedId() !== accountId) return;
        if (blob.size > 0) {
          this.misses.delete(key);
          sig.set(URL.createObjectURL(blob));
        } else {
          this.misses.set(key, this.now());
        }
      })
      .catch(() => {
        if (this.accounts.selectedId() === accountId) this.misses.set(key, this.now());
      })
      .finally(() => this.inflight.delete(key));
  }
}
