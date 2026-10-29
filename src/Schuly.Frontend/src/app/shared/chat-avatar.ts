import { Component, computed, input } from '@angular/core';
import { PersonAvatar } from './person-avatar';

export interface ChatPerson {
  id: string | null;
  name: string;
}

/** One photo for a 1:1 chat, a small stack of two for groups and meetings. */
@Component({
  selector: 'app-chat-avatar',
  imports: [PersonAvatar],
  template: `
    @if (stacked()) {
      <span class="relative block size-10 shrink-0" aria-hidden="true">
        <app-person-avatar class="absolute top-0 left-0" size="sm" [userId]="people()[0].id" [name]="people()[0].name" />
        <app-person-avatar
          class="ring-background absolute right-0 bottom-0 rounded-full ring-2"
          size="sm"
          [userId]="people()[1].id"
          [name]="people()[1].name"
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
  readonly people = input<ChatPerson[]>([]);
  readonly oneOnOne = input(false);
  readonly title = input('');
  protected readonly stacked = computed(() => !this.oneOnOne() && this.people().length >= 2);
}
