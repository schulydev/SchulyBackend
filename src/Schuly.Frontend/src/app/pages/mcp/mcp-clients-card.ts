import { DOCUMENT } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { BrandIcon } from '../../shared/brand-icon';
import { lucideChevronDown, lucideCode, lucideMessageSquare, lucideMonitor, lucideTerminal } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmCollapsibleImports } from '@spartan-ng/helm/collapsible';
import { HlmTabsImports } from '@spartan-ng/helm/tabs';
import { mcpClientGuides, mcpUrl } from '../../core/mcp-clients';
import { CopyBlock } from '../../shared/copy-block';

@Component({
  selector: 'app-mcp-clients-card',
  imports: [BrandIcon, HlmButton, HlmCardImports, HlmCollapsibleImports, HlmTabsImports, NgIcon, CopyBlock],
  providers: [provideIcons({ lucideChevronDown, lucideCode, lucideMessageSquare, lucideMonitor, lucideTerminal })],
  template: `
    <section hlmCard>
      <div hlmCardHeader>
        <h2 hlmCardTitle>Set up your client</h2>
        <p hlmCardDescription>Pick the app you want to connect.</p>
      </div>
      <div hlmCardContent>
        <hlm-tabs tab="claude-code" class="w-full">
          <div class="overflow-x-auto pb-1">
            <hlm-tabs-list aria-label="Client">
              @for (guide of guides(); track guide.id) {
                <button hlmTabsTrigger [hlmTabsTrigger]="guide.id" class="px-2.5">
                  @if (guide.brand; as brand) {
                    <app-brand-icon [brand]="brand" />
                  } @else {
                    <ng-icon [name]="guide.icon" aria-hidden="true" />
                  }
                  {{ guide.name }}
                </button>
              }
            </hlm-tabs-list>
          </div>
          @for (guide of guides(); track guide.id) {
            <div [hlmTabsContent]="guide.id" class="mt-3 flex flex-col gap-4">
              <ol class="flex flex-col gap-2 text-sm">
                @for (step of guide.steps; track $index) {
                  <li class="flex gap-3">
                    <span
                      class="bg-muted text-foreground flex size-5 shrink-0 items-center justify-center rounded-full text-xs font-medium"
                      aria-hidden="true"
                      >{{ $index + 1 }}</span
                    >
                    <span>{{ step }}</span>
                  </li>
                }
              </ol>

              @if (installLinks()[guide.id]; as href) {
                <div>
                  <a hlmBtn [href]="href">{{ guide.installLink?.label }}</a>
                </div>
              }

              <div class="flex flex-col gap-1.5">
                <p class="text-muted-foreground text-xs font-medium">{{ guide.snippet.title }}</p>
                <app-copy-block [text]="guide.snippet.code" [label]="guide.name + ' ' + guide.snippet.title" />
              </div>

              @if (guide.note) {
                <p class="text-muted-foreground text-sm">{{ guide.note }}</p>
              }

              <p class="text-sm">
                <span class="font-medium">What happens next.</span>
                Your browser opens {{ host }} to sign in and approve access. You choose what the app may see.
              </p>

              <hlm-collapsible class="rounded-lg border">
                <button hlmCollapsibleTrigger hlmBtn variant="ghost" class="w-full justify-between">
                  Use an API key instead
                  <ng-icon name="lucideChevronDown" aria-hidden="true" />
                </button>
                <hlm-collapsible-content>
                  <div class="flex flex-col gap-3 p-3 pt-0">
                    <p class="text-muted-foreground text-sm">
                      Create a key in the API keys section below, then use it in place of the browser sign-in.
                      {{ guide.key.intro }}
                    </p>
                    @if (guide.key.snippet; as snippet) {
                      <app-copy-block [text]="snippet.code" [label]="guide.name + ' API key ' + snippet.title" />
                    }
                  </div>
                </hlm-collapsible-content>
              </hlm-collapsible>
            </div>
          }
        </hlm-tabs>
      </div>
    </section>
  `,
})
export class McpClientsCard {
  private readonly document = inject(DOCUMENT);
  private readonly sanitizer = inject(DomSanitizer);

  protected readonly host = this.document.location.host;
  protected readonly guides = computed(() => mcpClientGuides(mcpUrl(this.document.location.origin)));

  /** Custom-scheme deep links are built by us from the server URL only, so they are trusted explicitly. */
  protected readonly installLinks = computed(() => {
    const links: Record<string, SafeUrl> = {};
    for (const guide of this.guides()) {
      if (guide.installLink) links[guide.id] = this.sanitizer.bypassSecurityTrustUrl(guide.installLink.href);
    }
    return links;
  });
}
