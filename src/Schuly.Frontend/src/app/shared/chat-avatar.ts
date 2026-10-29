import { Component, DestroyRef, ElementRef, afterNextRender, computed, effect, inject, input, signal } from '@angular/core';
import { HlmAvatarImports } from '@spartan-ng/helm/avatar';
import { TeamsStore } from '../stores/teams.store';
import { PersonAvatar } from './person-avatar';

export interface ChatPerson {
  id: string | null;
  name: string;
}

/** Graph has no group photo, so a group icon is built from members; this many are asked for photos. */
const PHOTO_CANDIDATES = 4;

/**
 * Icon of a chat, in this order:
 * 1. the chat's own image, when the data has one;
 * 2. a 1:1 chat shows the other person;
 * 3. a group prefers members who have a photo (never the signed-in user): two photos are stacked, exactly one is shown on its own;
 * 4. initials only when no member has a photo.
 */
@Component({
  selector: 'app-chat-avatar',
  imports: [HlmAvatarImports, PersonAvatar],
  template: `
    @if (image(); as src) {
      <hlm-avatar size="lg" aria-hidden="true">
        <img [src]="src" alt="" width="40" height="40" loading="lazy" decoding="async" class="size-full rounded-full object-cover" />
      </hlm-avatar>
    } @else if (shown().length === 2) {
      <span class="relative block size-10 shrink-0" aria-hidden="true">
        <app-person-avatar class="absolute top-0 left-0" size="sm" [userId]="shown()[0].id" [name]="shown()[0].name" />
        <app-person-avatar
          class="ring-background absolute right-0 bottom-0 rounded-full ring-2"
          size="sm"
          [userId]="shown()[1].id"
          [name]="shown()[1].name"
        />
      </span>
    } @else {
      <span aria-hidden="true">
        <app-person-avatar size="lg" [userId]="shown()[0]?.id ?? null" [name]="shown()[0]?.name ?? title()" />
      </span>
    }
  `,
  host: { class: 'inline-flex shrink-0' },
})
export class ChatAvatar {
  private readonly store = inject(TeamsStore);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly people = input<ChatPerson[]>([]);
  readonly oneOnOne = input(false);
  readonly title = input('');
  readonly image = input<string | null>(null);

  private readonly visible = signal(false);

  /** Everyone but the signed-in user (the store already leaves them out; this keeps the rule true for any caller). */
  private readonly others = computed(() => {
    const me = this.store.meId();
    const others = this.people().filter((p) => !me || p.id !== me);
    return others.length ? others : this.people();
  });

  protected readonly shown = computed<ChatPerson[]>(() => {
    const others = this.others();
    if (this.oneOnOne()) return others.slice(0, 1);
    const candidates = others.slice(0, PHOTO_CANDIDATES);
    const withPhoto = candidates.filter((p) => !!this.store.photoFor(p.id));
    if (withPhoto.length >= 2) return withPhoto.slice(0, 2);
    if (withPhoto.length === 1) return withPhoto;
    return others.slice(0, 2);
  });

  constructor() {
    afterNextRender(() => {
      if (typeof IntersectionObserver === 'undefined') {
        this.visible.set(true);
        return;
      }
      const observer = new IntersectionObserver((entries) => {
        if (entries.some((e) => e.isIntersecting)) {
          this.visible.set(true);
          observer.disconnect();
        }
      });
      observer.observe(this.element.nativeElement);
      this.destroyRef.onDestroy(() => observer.disconnect());
    });
    // The store de-duplicates requests per (account, user).
    effect(() => {
      if (!this.visible() || this.image()) return;
      for (const p of this.others().slice(0, PHOTO_CANDIDATES)) if (p.id) this.store.requestPhoto(p.id);
    });
  }
}
