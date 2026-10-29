import { Component, DestroyRef, ElementRef, afterNextRender, computed, effect, inject, input, signal } from '@angular/core';
import { TeamsStore } from '../stores/teams.store';
import { PersonAvatar } from './person-avatar';

export interface ChatPerson {
  id: string | null;
  name: string;
}

/** Graph has no group photo, so a group icon is built from members; this many are asked for photos. */
const PHOTO_CANDIDATES = 4;

/**
 * One photo for a 1:1 chat. Groups and meetings show two overlapping members (never me), preferring members who have a
 * photo: the first few get a photo request and the first two that resolved with an image are used, initials only when nobody has one.
 */
@Component({
  selector: 'app-chat-avatar',
  imports: [PersonAvatar],
  template: `
    @if (stacked()) {
      <span class="relative block size-10 shrink-0" aria-hidden="true">
        <app-person-avatar class="absolute top-0 left-0" size="sm" [userId]="pair()[0].id" [name]="pair()[0].name" />
        <app-person-avatar
          class="ring-background absolute right-0 bottom-0 rounded-full ring-2"
          size="sm"
          [userId]="pair()[1].id"
          [name]="pair()[1].name"
        />
      </span>
    } @else {
      <span aria-hidden="true">
        <app-person-avatar size="lg" [userId]="people()[0]?.id ?? null" [name]="people()[0]?.name ?? title()" />
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
  protected readonly stacked = computed(() => !this.oneOnOne() && this.people().length >= 2);

  private readonly visible = signal(false);

  protected readonly pair = computed<ChatPerson[]>(() => {
    const candidates = this.people().slice(0, PHOTO_CANDIDATES);
    const withPhoto = candidates.filter((p) => !!this.store.photoFor(p.id));
    const rest = this.people().filter((p) => !withPhoto.includes(p));
    return [...withPhoto, ...rest].slice(0, 2);
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
      if (!this.visible() || !this.stacked()) return;
      for (const p of this.people().slice(0, PHOTO_CANDIDATES)) if (p.id) this.store.requestPhoto(p.id);
    });
  }
}
