import { Component, DestroyRef, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideDownload, lucideExternalLink, lucideFileX, lucideX } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmSpinner } from '@spartan-ng/helm/spinner';

/** A file the viewer can show: `load` fetches the bytes with the signed-in session, exactly like a download does. */
export interface ViewerFile {
  name: string;
  load: () => Promise<Blob>;
}

type Rendered =
  | { kind: 'pdf'; url: string; safe: SafeResourceUrl }
  | { kind: 'image'; url: string }
  | { kind: 'text'; url: string; text: string }
  | { kind: 'none'; url: null };

const MAX_TEXT = 1024 * 1024;

const startsWith = (bytes: Uint8Array, signature: (number | null)[], offset = 0) => signature.every((b, i) => b === null || bytes[offset + i] === b);

/** What the bytes really are. The declared type is only trusted for plain text; markup (html, svg, xml) is never rendered. */
export function sniff(bytes: Uint8Array, declared: string): { kind: 'pdf' | 'image' | 'text' | 'none'; type: string } {
  const head = bytes.subarray(0, 1024);
  for (let i = 0; i + 4 <= head.length; i++) {
    if (startsWith(head, [0x25, 0x50, 0x44, 0x46], i)) return { kind: 'pdf', type: 'application/pdf' };
  }
  if (startsWith(bytes, [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])) return { kind: 'image', type: 'image/png' };
  if (startsWith(bytes, [0xff, 0xd8, 0xff])) return { kind: 'image', type: 'image/jpeg' };
  if (startsWith(bytes, [0x47, 0x49, 0x46, 0x38])) return { kind: 'image', type: 'image/gif' };
  if (startsWith(bytes, [0x52, 0x49, 0x46, 0x46]) && startsWith(bytes, [0x57, 0x45, 0x42, 0x50], 8)) return { kind: 'image', type: 'image/webp' };
  const type = declared.split(';')[0].trim().toLowerCase();
  if (type === 'text/plain' || type === 'application/octet-stream' || type === '') {
    const sample = bytes.subarray(0, 8192);
    // eslint-disable-next-line no-control-regex
    const binary = sample.some((b) => b === 0) || /[\u0001-\u0008\u000e-\u001f]/.test(new TextDecoder('utf-8').decode(sample));
    if (!binary && type === 'text/plain') return { kind: 'text', type: 'text/plain' };
    if (!binary) {
      const text = new TextDecoder('utf-8').decode(sample).trimStart();
      if (text.length && !text.startsWith('<') && !text.startsWith('�')) return { kind: 'text', type: 'text/plain' };
    }
  }
  return { kind: 'none', type: 'application/octet-stream' };
}

/** Shared in-browser viewer for PDFs, images and plain text. Everything else is download-only, and nothing is ever navigated to a server URL. */
@Component({
  selector: 'app-file-viewer',
  imports: [HlmButton, HlmDialogImports, HlmSpinner, NgIcon],
  providers: [provideIcons({ lucideDownload, lucideExternalLink, lucideFileX, lucideX })],
  template: `
    <hlm-dialog [state]="file() ? 'open' : 'closed'" (closed)="closed.emit()">
      <hlm-dialog-content
        *hlmDialogPortal="let ctx"
        [showCloseButton]="false"
        class="h-dvh max-h-none w-screen max-w-none grid-rows-[auto_minmax(0,1fr)] gap-0 overflow-hidden rounded-none p-0 sm:h-[calc(100dvh-2rem)] sm:w-[calc(100vw-2rem)] sm:max-w-none sm:rounded-xl"
      >
        <header class="flex min-h-14 flex-wrap items-center gap-2 border-b px-4 py-2">
          <h2 hlmDialogTitle class="min-w-0 flex-1 truncate text-base font-semibold">{{ file()?.name }}</h2>
          <p hlmDialogDescription class="sr-only">File preview</p>
          <button hlmBtn variant="ghost" size="sm" type="button" [disabled]="!blob()" (click)="download()">
            <ng-icon name="lucideDownload" class="text-base" aria-hidden="true" />
            <span class="max-sm:sr-only">Download</span>
          </button>
          @if (rendered(); as r) {
            @if (r.url) {
              <button hlmBtn variant="ghost" size="sm" type="button" (click)="openInTab(r.url)">
                <ng-icon name="lucideExternalLink" class="text-base" aria-hidden="true" />
                <span class="max-sm:sr-only">Open in new tab</span>
                <span class="sr-only">(opens in a new tab)</span>
              </button>
            }
          }
          <button hlmBtn variant="ghost" size="sm" type="button" (click)="closed.emit()">
            <ng-icon name="lucideX" class="text-base" aria-hidden="true" />
            <span class="max-sm:sr-only">Close</span>
          </button>
        </header>

        <div class="bg-muted/40 relative flex min-h-0 min-w-0 flex-col" [attr.aria-busy]="loading()">
          @if (loading()) {
            <div class="text-muted-foreground m-auto flex items-center gap-2 text-sm" role="status">
              <hlm-spinner aria-hidden="true" />
              Loading file
            </div>
          } @else if (failed()) {
            <p class="text-muted-foreground m-auto text-sm" role="alert">Could not load this file. Try again.</p>
          } @else {
            @switch (rendered()?.kind) {
              @case ('pdf') {
                @if (pdf(); as safe) {
                  <iframe class="size-full border-0 bg-white" [src]="safe" [title]="'Preview of ' + file()?.name"></iframe>
                }
              }
              @case ('image') {
                <div class="flex min-h-0 flex-1 items-center justify-center overflow-auto p-4">
                  <img [src]="imageUrl()" [alt]="'Preview of ' + file()?.name" class="max-h-full max-w-full object-contain" />
                </div>
              }
              @case ('text') {
                <pre class="min-h-0 flex-1 overflow-auto p-4 text-sm whitespace-pre-wrap wrap-anywhere" tabindex="0" [attr.aria-label]="'Contents of ' + file()?.name">{{ text() }}</pre>
              }
              @default {
                <div class="m-auto flex flex-col items-center gap-3 p-6 text-center">
                  <ng-icon name="lucideFileX" class="text-muted-foreground text-3xl" aria-hidden="true" />
                  <p class="font-medium">Preview not available</p>
                  <p class="text-muted-foreground text-sm">This file type cannot be shown here.</p>
                  <button hlmBtn type="button" (click)="download()">
                    <ng-icon name="lucideDownload" aria-hidden="true" />
                    Download
                  </button>
                </div>
              }
            }
          }
        </div>
      </hlm-dialog-content>
    </hlm-dialog>
  `,
})
export class FileViewer {
  private readonly sanitizer = inject(DomSanitizer);

  readonly file = input<ViewerFile | null>(null);
  readonly closed = output<void>();

  protected readonly loading = signal(false);
  protected readonly failed = signal(false);
  protected readonly rendered = signal<Rendered | null>(null);
  protected readonly blob = signal<Blob | null>(null);

  protected readonly pdf = computed(() => {
    const r = this.rendered();
    return r?.kind === 'pdf' ? r.safe : null;
  });
  protected readonly imageUrl = computed(() => {
    const r = this.rendered();
    return r?.kind === 'image' ? r.url : '';
  });
  protected readonly text = computed(() => {
    const r = this.rendered();
    return r?.kind === 'text' ? r.text : '';
  });

  private seq = 0;
  private readonly urls = new Set<string>();

  constructor() {
    effect(() => {
      const file = this.file();
      untracked(() => void this.show(file));
    });
    inject(DestroyRef).onDestroy(() => this.reset());
  }

  private reset(): void {
    this.seq++;
    for (const url of this.urls) URL.revokeObjectURL(url);
    this.urls.clear();
    this.rendered.set(null);
    this.blob.set(null);
    this.failed.set(false);
    this.loading.set(false);
  }

  private async show(file: ViewerFile | null): Promise<void> {
    this.reset();
    if (!file) return;
    const seq = this.seq;
    this.loading.set(true);
    try {
      const raw = await file.load();
      if (seq !== this.seq) return;
      const bytes = new Uint8Array(await raw.arrayBuffer());
      if (seq !== this.seq) return;
      const { kind, type } = sniff(bytes, raw.type);
      this.blob.set(raw);
      if (kind === 'none') {
        this.rendered.set({ kind: 'none', url: null });
      } else if (kind === 'text') {
        const text = new TextDecoder('utf-8').decode(bytes.subarray(0, MAX_TEXT));
        const url = this.make(new Blob([bytes], { type }));
        this.rendered.set({ kind: 'text', url, text });
      } else {
        // Re-typed from the verified bytes, never from the server's header.
        const url = this.make(new Blob([bytes], { type }));
        this.rendered.set(kind === 'pdf' ? { kind, url, safe: this.sanitizer.bypassSecurityTrustResourceUrl(url) } : { kind, url });
      }
    } catch {
      if (seq === this.seq) this.failed.set(true);
    } finally {
      if (seq === this.seq) this.loading.set(false);
    }
  }

  private make(blob: Blob): string {
    const url = URL.createObjectURL(blob);
    this.urls.add(url);
    return url;
  }

  protected openInTab(url: string): void {
    window.open(url, '_blank', 'noopener');
  }

  /** Saved as opaque bytes through a temporary link, so the browser never renders it. */
  protected download(): void {
    const blob = this.blob();
    const name = this.file()?.name;
    if (!blob || !name) return;
    const url = URL.createObjectURL(new Blob([blob], { type: 'application/octet-stream' }));
    const link = Object.assign(document.createElement('a'), { href: url, download: name, rel: 'noopener', hidden: true });
    document.body.append(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 60_000);
  }
}
