import { Component, inject, input } from '@angular/core';
import { ThemeStore } from '../../stores/theme.store';

@Component({
  selector: 'app-auth-layout',
  host: { class: 'bg-sidebar text-foreground flex min-h-svh' },
  template: `
    <div
      class="mx-auto flex w-[calc(100%-40px)] max-w-360 flex-col min-[900px]:w-[calc(100%-2*clamp(2rem,1.43rem+2.86vw,4rem))]"
    >
      <a
        href="https://schuly.dev"
        class="flex items-center gap-2.5 self-start pt-6 text-base font-semibold tracking-tight min-[900px]:pt-8 min-[900px]:text-[17px]"
      >
        <img src="app_icon.png" alt="" class="size-7 rounded-md" />
        <span>Schuly</span>
      </a>

      <div class="grid flex-1 gap-4 min-[900px]:grid-cols-2">
        <main class="flex flex-col items-center py-8 min-[900px]:justify-center min-[900px]:py-6">
          <div class="rise-in flex w-full flex-col gap-8 min-[900px]:max-w-100 min-[900px]:gap-7">
            <header class="flex flex-col gap-2.5 text-center min-[900px]:gap-3.5">
              <h1
                class="text-4xl leading-[1.05] font-semibold tracking-tighter text-balance min-[900px]:text-5xl"
              >
                {{ title() }}
              </h1>
              @if (subtitle()) {
                <p class="text-muted-foreground text-balance min-[900px]:text-base">
                  {{ subtitle() }}
                </p>
              }
            </header>
            <ng-content />
          </div>
        </main>

        <div class="hidden items-center min-[900px]:flex">
          <div
            class="relative h-[min(44rem,calc(100svh-11rem))] w-full overflow-hidden [mask-image:linear-gradient(to_right,black_60%,transparent_100%)]"
          >
            <img
              [src]="theme.dark() ? 'screens/overview-dark.png' : 'screens/overview-light.png'"
              alt="The Schuly web app showing the overview with grades, upcoming tests and absences"
              class="absolute top-1/2 left-0 w-[64rem] max-w-none -translate-y-1/2 rounded-l-xl border border-r-0 shadow-sm"
            />
          </div>
        </div>
      </div>

      <footer
        class="text-muted-foreground flex flex-col gap-3 py-6 text-sm sm:flex-row sm:items-center sm:justify-between"
      >
        <nav aria-label="Legal">
          <ul class="flex flex-wrap gap-x-4 gap-y-1">
            @for (link of legal; track link.href) {
              <li>
                <a
                  class="hover:text-foreground underline-offset-4 hover:underline"
                  [href]="link.href"
                  target="_blank"
                  rel="noopener"
                  >{{ link.label }}</a
                >
              </li>
            }
          </ul>
        </nav>
        <p>© {{ year }} Schuly</p>
      </footer>
    </div>
  `,
})
export class AuthLayout {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();

  protected readonly theme = inject(ThemeStore);
  protected readonly year = new Date().getFullYear();
  protected readonly legal = [
    { label: 'Privacy', href: 'https://schuly.dev/privacy' },
    { label: 'Terms', href: 'https://schuly.dev/terms' },
    { label: 'Imprint', href: 'https://schuly.dev/impressum' },
    { label: 'Contact', href: 'mailto:hello@schuly.dev' },
  ];
}
