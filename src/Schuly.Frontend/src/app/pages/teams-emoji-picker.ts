import { Component, ElementRef, OnInit, afterNextRender, computed, inject, output, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideDog, lucideFlag, lucideHash, lucideLightbulb, lucidePlane, lucideSearch, lucideSmile, lucideStar, lucideTrophy, lucideUsers, lucideUtensils } from '@ng-icons/lucide';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmSpinner } from '@spartan-ng/helm/spinner';
import { HlmTabsImports } from '@spartan-ng/helm/tabs';
import { PickerGroup, filterPickerEmoji, loadEmojiGroups } from '../core/emoji';
import { allowedMediaUrl } from '../core/chat-media';
import { Gif, TeamsApi } from '../core/teams-api';
import { Catalogue, CatalogueEmoji, EmojiRef, searchCatalogue } from '../core/teams-emoji';
import { EmojiView } from '../shared/emoji-view';

const GROUP_ICONS: Record<string, string> = {
  'smileys-emotion': 'lucideSmile',
  'people-body': 'lucideUsers',
  'animals-nature': 'lucideDog',
  'food-drink': 'lucideUtensils',
  'travel-places': 'lucidePlane',
  activities: 'lucideTrophy',
  objects: 'lucideLightbulb',
  symbols: 'lucideHash',
  flags: 'lucideFlag',
};

/**
 * Emoji tab and a GIF tab (only when GIF search is configured). The emoji tab shows Teams' own categories from the catalogue
 * (animated on hover) and falls back to categorized unicode when the server has no catalogue.
 */
@Component({
  selector: 'app-emoji-picker',
  imports: [NgIcon, HlmInput, HlmSpinner, HlmTabsImports, EmojiView],
  providers: [provideIcons({ lucideDog, lucideFlag, lucideHash, lucideLightbulb, lucidePlane, lucideSearch, lucideSmile, lucideStar, lucideTrophy, lucideUsers, lucideUtensils })],
  template: `
    <hlm-tabs tab="emoji" class="w-full">
      <hlm-tabs-list aria-label="Emoji or GIF" class="w-full">
        <button hlmTabsTrigger="emoji">Emoji</button>
        @if (api.gifsAvailable() === true) {
          <button hlmTabsTrigger="gifs">GIFs</button>
        }
      </hlm-tabs-list>

      <div hlmTabsContent="emoji" class="mt-2 flex flex-col gap-2">
        <label class="sr-only" for="emoji-search">Search emoji</label>
        <input hlmInput id="emoji-search" type="search" autocomplete="off" placeholder="Search emoji" [value]="query()" (input)="query.set($any($event.target).value)" />
        @if (!query().trim() && categories().length > 1) {
          <div class="flex max-h-16 flex-wrap gap-1 overflow-y-auto" role="group" aria-label="Emoji categories">
            @for (c of categories(); track c.key) {
              <button
                type="button"
                class="hover:bg-accent focus-visible:outline-ring aria-pressed:bg-accent flex h-8 min-w-8 items-center justify-center rounded-md px-1.5 text-xs focus-visible:outline-2"
                [attr.aria-pressed]="c.key === active()"
                [title]="c.label"
                (click)="active.set(c.key)"
              >
                @if (c.icon) {
                  <ng-icon [name]="c.icon" aria-hidden="true" />
                  <span class="sr-only">{{ c.label }}</span>
                } @else {
                  <span class="max-w-20 truncate">{{ c.label }}</span>
                }
              </button>
            }
          </div>
        }
        <div class="h-56 overflow-y-auto" tabindex="0" aria-label="Emoji" role="region">
          @if (loading()) {
            <p class="text-muted-foreground flex items-center gap-2 p-2 text-sm" role="status"><hlm-spinner aria-hidden="true" />Loading emoji</p>
          } @else if (failed()) {
            <p class="text-muted-foreground p-2 text-sm" role="alert">Emoji could not be loaded.</p>
          } @else if (shownTeams().length || shownUnicode().length) {
            <ul class="grid grid-cols-8 gap-0.5">
              @for (t of shownTeams(); track t.id) {
                <li>
                  <button
                    type="button"
                    class="hover:bg-accent focus-visible:outline-ring flex size-9 items-center justify-center rounded-md focus-visible:outline-2"
                    [attr.aria-label]="t.label"
                    [title]="t.label"
                    (mouseenter)="hovered.set(t.id)"
                    (focus)="hovered.set(t.id)"
                    (click)="pickTeams(t)"
                  >
                    @if (hovered() === t.id) {
                      <app-emoji [emoji]="{ kind: 'teams', emoji: t }" [size]="28" />
                    } @else if (t.staticUrl) {
                      <img [src]="t.staticUrl" alt="" width="28" height="28" loading="lazy" decoding="async" referrerpolicy="no-referrer" />
                    } @else {
                      <span aria-hidden="true" class="text-xl">{{ t.alt }}</span>
                    }
                  </button>
                </li>
              }
              @for (e of shownUnicode(); track e.char) {
                <li>
                  <button type="button" class="hover:bg-accent focus-visible:outline-ring flex size-9 items-center justify-center rounded-md text-xl focus-visible:outline-2" [attr.aria-label]="e.label" [title]="e.label" (click)="pickUnicode(e.char, e.label)">
                    <span aria-hidden="true">{{ e.char }}</span>
                  </button>
                </li>
              }
            </ul>
          } @else {
            <p class="text-muted-foreground p-2 text-sm" role="status">No emoji found.</p>
          }
        </div>
      </div>

      @if (api.gifsAvailable() === true) {
        <div hlmTabsContent="gifs" class="mt-2 flex flex-col gap-2">
          <label class="sr-only" for="gif-search">Search GIFs</label>
          <input hlmInput id="gif-search" type="search" autocomplete="off" placeholder="Search GIFs" [value]="gifQuery()" (input)="onGifQuery($any($event.target).value)" />
          <div class="h-56 overflow-y-auto" tabindex="0" aria-label="GIFs" role="region">
            @if (gifsLoading()) {
              <p class="text-muted-foreground flex items-center gap-2 p-2 text-sm" role="status"><hlm-spinner aria-hidden="true" />Loading GIFs</p>
            } @else if (gifError()) {
              <p class="text-muted-foreground p-2 text-sm" role="alert">GIFs could not be loaded.</p>
            } @else if (!gifs().length) {
              <p class="text-muted-foreground p-2 text-sm" role="status">No GIFs found.</p>
            } @else {
              <ul class="grid grid-cols-2 gap-1">
                @for (g of gifs(); track g.id) {
                  <li>
                    <button type="button" class="hover:ring-ring focus-visible:outline-ring block w-full overflow-hidden rounded-md hover:ring-2 focus-visible:outline-2" (click)="gif.emit(g)">
                      <img [src]="g.previewUrl || g.url" [alt]="g.title || 'GIF'" referrerpolicy="no-referrer" loading="lazy" class="h-24 w-full object-cover" />
                    </button>
                  </li>
                }
              </ul>
            }
          </div>
        </div>
      }
    </hlm-tabs>
  `,
  host: { class: 'block w-full' },
})
export class EmojiPicker implements OnInit {
  protected readonly api = inject(TeamsApi);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);

  constructor() {
    // The popover overlay is a role=dialog the library leaves without a name.
    afterNextRender(() => this.element.nativeElement.closest('[role="dialog"]')?.setAttribute('aria-label', 'Emoji and GIF picker'));
  }

  readonly emoji = output<EmojiRef>();
  readonly gif = output<Gif>();

  protected readonly query = signal('');
  protected readonly active = signal('');
  protected readonly hovered = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  private readonly groups = signal<PickerGroup[]>([]);
  private readonly catalogue = signal<Catalogue | null>(null);

  protected readonly gifQuery = signal('');
  protected readonly gifs = signal<Gif[]>([]);
  protected readonly gifsLoading = signal(false);
  protected readonly gifError = signal(false);
  private gifTimer: ReturnType<typeof setTimeout> | undefined;
  private gifSeq = 0;

  protected readonly categories = computed(() => {
    const cat = this.catalogue();
    if (cat) return cat.categories.map((c) => ({ key: c.id, label: c.title, icon: null as string | null }));
    return this.groups().map((g) => ({ key: g.key, label: g.label, icon: GROUP_ICONS[g.key] ?? 'lucideSmile' }));
  });
  protected readonly shownTeams = computed<CatalogueEmoji[]>(() => {
    const cat = this.catalogue();
    if (!cat) return [];
    const q = this.query().trim();
    if (q) return searchCatalogue(cat, q);
    return cat.categories.find((c) => c.id === this.active())?.emojis ?? [];
  });
  protected readonly shownUnicode = computed(() => {
    if (this.catalogue()) return [];
    const q = this.query().trim();
    if (q) return filterPickerEmoji(this.groups(), q);
    return this.groups().find((g) => g.key === this.active())?.emoji ?? [];
  });

  ngOnInit(): void {
    void this.loadEmoji();
    void this.loadGifs(null);
  }

  private async loadEmoji(): Promise<void> {
    try {
      // The catalogue is optional: without it the picker is plain unicode.
      const catalogue = await this.api.catalogue();
      if (catalogue) {
        this.catalogue.set(catalogue);
        this.active.set(catalogue.categories[0].id);
      } else {
        const groups = await loadEmojiGroups();
        this.groups.set(groups);
        this.active.set(groups[0]?.key ?? '');
      }
    } catch {
      this.failed.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  protected onGifQuery(value: string): void {
    this.gifQuery.set(value);
    clearTimeout(this.gifTimer);
    this.gifTimer = setTimeout(() => void this.loadGifs(value.trim() || null), 300);
  }

  private async loadGifs(query: string | null): Promise<void> {
    const seq = ++this.gifSeq;
    this.gifsLoading.set(true);
    this.gifError.set(false);
    const result = await this.api.gifs(query);
    if (seq !== this.gifSeq) return;
    this.gifsLoading.set(false);
    // Only GIFs from the allow-listed public hosts are shown or sent.
    if (result.status === 'ok') this.gifs.set(result.items.filter((g) => allowedMediaUrl(g.url) && (!g.previewUrl || allowedMediaUrl(g.previewUrl))));
    else this.gifError.set(result.status === 'error');
  }

  protected pickUnicode(char: string, label: string): void {
    this.emoji.emit({ kind: 'unicode', char, label });
  }

  protected pickTeams(emoji: CatalogueEmoji): void {
    this.emoji.emit({ kind: 'teams', emoji });
  }
}
