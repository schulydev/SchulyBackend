import { Component } from '@angular/core';

@Component({
  selector: 'app-helply',
  template: `
    <div class="mx-auto flex w-full max-w-3xl flex-col items-center gap-3 p-4 py-16 text-center lg:p-6">
      <img src="helply-avatar.svg" alt="" class="size-16" />
      <h2 class="text-xl font-semibold">Helply is coming soon</h2>
      <p class="text-muted-foreground max-w-md text-sm">
        Helply will be your study companion inside Schuly. We are still building it.
      </p>
    </div>
  `,
})
export class Helply {}
