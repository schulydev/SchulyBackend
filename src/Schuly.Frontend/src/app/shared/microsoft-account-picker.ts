import { Component, inject } from '@angular/core';
import { HlmSelectImports } from '@spartan-ng/helm/select';
import { MicrosoftAccountsStore } from '../stores/microsoft-accounts.store';

@Component({
  selector: 'app-microsoft-account-picker',
  imports: [HlmSelectImports],
  template: `
    @if (accounts.accounts().length > 1) {
      <div class="flex items-center gap-2">
        <label for="microsoft-account" class="text-muted-foreground text-sm">Account</label>
        <hlm-select [value]="accounts.selectedId()" [itemToString]="label" (valueChange)="choose($event)">
          <hlm-select-trigger buttonId="microsoft-account">
            <hlm-select-value />
          </hlm-select-trigger>
          <hlm-select-content *hlmSelectPortal>
            <hlm-select-group>
              @for (account of accounts.accounts(); track account.id) {
                <hlm-select-item [value]="account.id">{{ account.displayName || account.email }}</hlm-select-item>
              }
            </hlm-select-group>
          </hlm-select-content>
        </hlm-select>
      </div>
    }
  `,
})
export class MicrosoftAccountPicker {
  protected readonly accounts = inject(MicrosoftAccountsStore);

  protected readonly label = (id: string): string => {
    const account = this.accounts.accounts().find((a) => a.id === id);
    return account?.displayName || account?.email || id;
  };

  protected choose(id: string | null | undefined): void {
    if (id) this.accounts.select(id);
  }
}
