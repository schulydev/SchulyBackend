import { Component, inject } from '@angular/core';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { MicrosoftAccountsStore } from '../stores/microsoft-accounts.store';

@Component({
  selector: 'app-microsoft-account-picker',
  imports: [HlmNativeSelectImports],
  template: `
    @if (accounts.accounts().length > 1) {
      <div class="flex items-center gap-2">
        <label for="microsoft-account" class="text-muted-foreground text-sm">Account</label>
        <hlm-native-select selectId="microsoft-account" [value]="accounts.selectedId()" (valueChange)="choose($event)">
          @for (account of accounts.accounts(); track account.id) {
            <option hlmNativeSelectOption [value]="account.id">
              {{ account.displayName || account.email }}
            </option>
          }
        </hlm-native-select>
      </div>
    }
  `,
})
export class MicrosoftAccountPicker {
  protected readonly accounts = inject(MicrosoftAccountsStore);

  protected choose(id: string | null | undefined): void {
    if (id) this.accounts.select(id);
  }
}
