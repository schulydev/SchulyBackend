import { Component, inject } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideExternalLink, lucideUnplug } from '@ng-icons/lucide';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmAlertDialogImports } from '@spartan-ng/helm/alert-dialog';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmSpinner } from '@spartan-ng/helm/spinner';
import { ConnectionsStore } from '../../stores/connections.store';

@Component({
  selector: 'app-connections-card',
  imports: [
    HlmAlertImports,
    HlmAlertDialogImports,
    HlmBadge,
    HlmButton,
    HlmCardImports,
    HlmSpinner,
    NgIcon,
  ],
  providers: [provideIcons({ lucideExternalLink, lucideUnplug }), ConnectionsStore],
  template: `
    <section hlmCard>
      <div hlmCardHeader>
        <h3 hlmCardTitle>Schools and accounts</h3>
        <p hlmCardDescription>The school and Microsoft 365 accounts Schuly syncs for you.</p>
      </div>
      <div hlmCardContent class="flex flex-col gap-4">
        @if (store.error()) {
          <div hlmAlert variant="destructive">
            <p hlmAlertDescription>{{ store.error() }}</p>
          </div>
        }

        @if (store.loading()) {
          <hlm-spinner aria-label="Loading connected accounts" />
        } @else if (store.connections().length) {
          <ul class="divide-y rounded-lg border">
            @for (connection of store.connections(); track connection.key) {
              <li class="flex items-center justify-between gap-2 px-3 py-2 text-sm">
                <span class="flex min-w-0 flex-col">
                  <span class="truncate font-medium">{{ connection.title }}</span>
                  @if (connection.detail) {
                    <span class="text-muted-foreground truncate text-xs">{{
                      connection.detail
                    }}</span>
                  }
                  <span hlmBadge variant="secondary" class="mt-1 self-start">{{
                    connection.sourceLabel
                  }}</span>
                </span>
                <hlm-alert-dialog>
                  <button
                    hlmAlertDialogTrigger
                    hlmBtn
                    variant="outline"
                    size="sm"
                    [disabled]="store.busy()"
                    [attr.aria-label]="'Disconnect ' + connection.title"
                  >
                    <ng-icon name="lucideUnplug" />
                    Disconnect
                  </button>
                  <hlm-alert-dialog-content *hlmAlertDialogPortal="let ctx">
                    <hlm-alert-dialog-header>
                      <h2 hlmAlertDialogTitle>Disconnect {{ connection.title }}?</h2>
                      <p hlmAlertDialogDescription>
                        Schuly stops syncing this account and removes its stored sign-in. You can
                        connect it again from the Schuly mobile app.
                      </p>
                    </hlm-alert-dialog-header>
                    <hlm-alert-dialog-footer>
                      <button hlmAlertDialogCancel>Cancel</button>
                      <button
                        hlmAlertDialogAction
                        variant="destructive"
                        (click)="store.disconnect(connection)"
                      >
                        Disconnect
                      </button>
                    </hlm-alert-dialog-footer>
                  </hlm-alert-dialog-content>
                </hlm-alert-dialog>
              </li>
            }
          </ul>
        } @else {
          <p class="text-muted-foreground text-sm">No schools connected yet.</p>
        }

        <p class="text-muted-foreground text-sm">
          You connect a school or Microsoft 365 account in the Schuly mobile app, because some
          schools need an in-app browser to sign in.
          <a
            class="text-foreground inline-flex items-center gap-1 underline underline-offset-4"
            href="https://schuly.dev/#download"
            target="_blank"
            rel="noopener"
          >
            Get the app
            <ng-icon name="lucideExternalLink" aria-hidden="true" />
            <span class="sr-only">(opens in a new tab)</span>
          </a>
        </p>
      </div>
    </section>
  `,
})
export class ConnectionsCard {
  protected readonly store = inject(ConnectionsStore);
}
