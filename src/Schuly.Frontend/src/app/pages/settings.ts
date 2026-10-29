import { Component, inject } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideDownload,
  lucideMonitor,
  lucideMoon,
  lucideSun,
  lucideTrash2,
} from '@ng-icons/lucide';
import { HlmAlertDialogImports } from '@spartan-ng/helm/alert-dialog';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmSpinner } from '@spartan-ng/helm/spinner';
import { HlmToggleGroupImports } from '@spartan-ng/helm/toggle-group';
import { Theme, ThemeStore } from '../stores/theme.store';
import { AccountCard } from './settings/account-card';
import { SecurityCard } from './settings/security-card';
import { ConnectionsCard } from './settings/connections-card';
import { UserStore } from '../stores/user.store';

@Component({
  selector: 'app-settings',
  imports: [
    AccountCard,
    SecurityCard,
    ConnectionsCard,
    HlmCardImports,
    HlmButton,
    HlmToggleGroupImports,
    HlmAlertDialogImports,
    HlmSpinner,
    NgIcon,
  ],
  providers: [provideIcons({ lucideSun, lucideMoon, lucideMonitor, lucideDownload, lucideTrash2 })],
  template: `
    <div class="mx-auto flex w-full max-w-3xl flex-col gap-6 p-4 lg:p-6">
      <app-account-card />
      <app-security-card />
      <app-connections-card />

      <section hlmCard>
        <div hlmCardHeader>
          <h3 hlmCardTitle>Appearance</h3>
          <p hlmCardDescription>Choose how Schuly looks on this device.</p>
        </div>
        <div hlmCardContent>
          <hlm-toggle-group
            type="single"
            variant="outline"
            [nullable]="false"
            [value]="theme.theme()"
            (valueChange)="setTheme($event)"
            aria-label="Theme"
          >
            <button hlmToggleGroupItem value="light" class="gap-2 px-3">
              <ng-icon name="lucideSun" /> Light
            </button>
            <button hlmToggleGroupItem value="dark" class="gap-2 px-3">
              <ng-icon name="lucideMoon" /> Dark
            </button>
            <button hlmToggleGroupItem value="system" class="gap-2 px-3">
              <ng-icon name="lucideMonitor" /> System
            </button>
          </hlm-toggle-group>
        </div>
      </section>

      <section hlmCard>
        <div hlmCardHeader>
          <h3 hlmCardTitle>Your data</h3>
          <p hlmCardDescription>Download everything Schuly stores about you as a JSON file.</p>
        </div>
        <div hlmCardFooter>
          <button hlmBtn variant="outline" [disabled]="user.busy()" (click)="user.exportData()">
            @if (user.busy()) {
              <hlm-spinner />
            } @else {
              <ng-icon name="lucideDownload" />
            }
            Export data
          </button>
        </div>
      </section>

      <section hlmCard class="border-destructive/40">
        <div hlmCardHeader>
          <h3 hlmCardTitle>Delete account</h3>
          <p hlmCardDescription>
            Permanently delete your Schuly account and all synced school data. This cannot be
            undone.
          </p>
        </div>
        <div hlmCardFooter>
          <hlm-alert-dialog>
            <button hlmAlertDialogTrigger hlmBtn variant="destructive" [disabled]="user.busy()">
              <ng-icon name="lucideTrash2" />
              Delete account
            </button>
            <hlm-alert-dialog-content *hlmAlertDialogPortal="let ctx">
              <hlm-alert-dialog-header>
                <h2 hlmAlertDialogTitle>Delete your account?</h2>
                <p hlmAlertDialogDescription>
                  This permanently deletes your Schuly account, your linked school accounts and
                  every grade, exam, absence and document synced to the server.
                </p>
              </hlm-alert-dialog-header>
              <hlm-alert-dialog-footer>
                <button hlmAlertDialogCancel>Cancel</button>
                <button hlmAlertDialogAction variant="destructive" (click)="user.deleteAccount()">
                  Delete account
                </button>
              </hlm-alert-dialog-footer>
            </hlm-alert-dialog-content>
          </hlm-alert-dialog>
        </div>
      </section>
    </div>
  `,
})
export class Settings {
  protected readonly user = inject(UserStore);
  protected readonly theme = inject(ThemeStore);

  protected setTheme(value: unknown): void {
    if (value === 'light' || value === 'dark' || value === 'system')
      this.theme.setTheme(value as Theme);
  }
}
