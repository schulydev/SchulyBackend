import { Component, DestroyRef, ElementRef, afterNextRender, computed, inject, input, signal } from '@angular/core';
import { HlmAvatarImports } from '@spartan-ng/helm/avatar';
import { initials } from '../core/initials';
import { SenderPhotos } from '../core/sender-photos';

/** Avatar of a mail sender: the account's photo for the address once the avatar scrolls into view, initials otherwise. */
@Component({
  selector: 'app-sender-avatar',
  imports: [HlmAvatarImports],
  template: `
    <hlm-avatar [size]="size()">
      @if (url(); as src) {
        <img [src]="src" alt="" width="48" height="48" decoding="async" class="size-full rounded-full object-cover" />
      } @else {
        <span hlmAvatarFallback class="text-foreground">{{ letters() }}</span>
      }
    </hlm-avatar>
  `,
  host: { class: 'inline-flex shrink-0', 'aria-hidden': 'true' },
})
export class SenderAvatar {
  private readonly photos = inject(SenderPhotos);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly email = input<string | null>(null);
  readonly name = input('');
  readonly size = input<'default' | 'sm' | 'lg'>('lg');

  private readonly visible = signal(false);
  protected readonly letters = computed(() => initials(this.name()) || '?');
  protected readonly url = computed(() => {
    if (!this.visible() || !this.email()) return null;
    return this.photos.photo(this.email())();
  });

  constructor() {
    afterNextRender(() => {
      if (typeof IntersectionObserver === 'undefined') return this.visible.set(true);
      const observer = new IntersectionObserver((entries) => {
        if (entries.some((e) => e.isIntersecting)) {
          this.visible.set(true);
          observer.disconnect();
        }
      });
      observer.observe(this.element.nativeElement);
      this.destroyRef.onDestroy(() => observer.disconnect());
    });
  }
}
