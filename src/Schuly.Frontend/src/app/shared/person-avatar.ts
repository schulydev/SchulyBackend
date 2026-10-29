import { Component, DestroyRef, ElementRef, afterNextRender, computed, effect, inject, input, signal } from '@angular/core';
import { HlmAvatarImports } from '@spartan-ng/helm/avatar';
import { initials } from '../core/initials';
import { ChatStore } from '../stores/chat.store';

/** Profile photo of a chat participant, fetched lazily once the avatar scrolls into view, with an initials fallback. */
@Component({
  selector: 'app-person-avatar',
  imports: [HlmAvatarImports],
  template: `
    <hlm-avatar [size]="size()">
      @if (url(); as src) {
        <img [src]="src" alt="" width="64" height="64" loading="lazy" decoding="async" class="size-full rounded-full object-cover" />
      } @else {
        <span hlmAvatarFallback class="text-foreground">{{ letters() }}</span>
      }
    </hlm-avatar>
  `,
  host: { class: 'inline-flex shrink-0' },
})
export class PersonAvatar {
  private readonly store = inject(ChatStore);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly userId = input<string | null>(null);
  readonly name = input('');
  readonly size = input<'default' | 'sm' | 'lg'>('default');

  private readonly visible = signal(false);
  protected readonly letters = computed(() => initials(this.name()) || '?');
  protected readonly url = computed(() => this.store.photoFor(this.userId()));

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
      const id = this.userId();
      if (id && this.visible()) this.store.requestPhoto(id);
    });
  }
}
