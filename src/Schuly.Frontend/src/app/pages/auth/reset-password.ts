import { Component, inject, input, signal } from '@angular/core';
import { form, FormField, FormRoot, minLength, required } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmSpinner } from '@spartan-ng/helm/spinner';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/auth-api';
import { authErrorMessage } from '../../stores/auth.store';
import { AuthLayout } from './auth-layout';

@Component({
  selector: 'app-reset-password',
  imports: [
    FormRoot,
    FormField,
    RouterLink,
    HlmAlertImports,
    HlmButton,
    HlmCardImports,
    HlmFieldImports,
    HlmInput,
    HlmSpinner,
    AuthLayout,
  ],
  template: `
    <app-auth-layout
      title="Choose a new password."
      subtitle="You're signed out everywhere once it's set."
    >
      <section hlmCard>
        <div hlmCardContent>
          @if (done()) {
            <div hlmAlert>
              <h4 hlmAlertTitle>Password changed</h4>
              <p hlmAlertDescription>Sign in with your new password.</p>
            </div>
          } @else if (!token()) {
            <div hlmAlert variant="destructive">
              <h4 hlmAlertTitle>This link is incomplete</h4>
              <p hlmAlertDescription>Open the link from the email again, or request a new one.</p>
            </div>
          } @else {
            <form [formRoot]="reset" class="flex flex-col gap-4">
              <div hlmField>
                <label hlmFieldLabel for="new-password">New password</label>
                <input
                  hlmInput
                  id="new-password"
                  type="password"
                  autocomplete="new-password"
                  [formField]="reset.password"
                />
                <p hlmFieldDescription>At least 8 characters.</p>
              </div>
              @if (error()) {
                <hlm-field-error>{{ error() }}</hlm-field-error>
              }
              <button hlmBtn size="lg" type="submit" [disabled]="reset().submitting()">
                @if (reset().submitting()) {
                  <hlm-spinner />
                }
                Set password
              </button>
            </form>
          }
        </div>
      </section>
      <a hlmBtn variant="link" [routerLink]="done() ? '/login' : '/forgot-password'">{{
        done() ? 'Sign in' : 'Request a new link'
      }}</a>
    </app-auth-layout>
  `,
})
export class ResetPassword {
  readonly token = input<string>();

  private readonly api = inject(AuthApi);
  protected readonly done = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly model = signal({ password: '' });
  protected readonly reset = form(
    this.model,
    (path) => {
      required(path.password);
      minLength(path.password, 8);
    },
    { submission: { action: () => this.submit() } },
  );

  private async submit(): Promise<undefined> {
    this.error.set(null);
    try {
      await firstValueFrom(this.api.resetPassword(this.token() ?? '', this.model().password));
      this.done.set(true);
    } catch (error) {
      this.error.set(
        authErrorMessage(error, 'This link has expired or was already used. Request a new one.'),
      );
    }
    return undefined;
  }
}
