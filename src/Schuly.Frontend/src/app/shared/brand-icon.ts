import { Component, computed, input } from '@angular/core';
import { siClaude, siCursor, siGithubcopilot, siWindsurf } from 'simple-icons';

/**
 * Monochrome client logos from Simple Icons (CC0 icon data), drawn in currentColor so they follow the theme. Only brands whose guidelines allow a plain
 * mark that refers to the product are here; OpenAI (ChatGPT, Codex), Microsoft (VS Code) and Google (Gemini) logos are left out and get a neutral icon.
 */
const BRANDS = { claude: siClaude, cursor: siCursor, githubcopilot: siGithubcopilot, windsurf: siWindsurf } as const;

export type BrandName = keyof typeof BRANDS;

@Component({
  selector: 'app-brand-icon',
  host: { class: 'inline-flex size-4 shrink-0', 'aria-hidden': 'true' },
  template: `<svg viewBox="0 0 24 24" class="size-full" fill="currentColor" focusable="false"><path [attr.d]="path()" /></svg>`,
})
export class BrandIcon {
  readonly brand = input.required<BrandName>();
  protected readonly path = computed(() => BRANDS[this.brand()].path);
}
