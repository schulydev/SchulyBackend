import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmSpinner } from '@spartan-ng/helm/spinner';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/auth-api';
import { AuthStore } from '../../stores/auth.store';
import { AuthLayout } from './auth-layout';

@Component({
  selector: 'app-verify-email',
  imports: [RouterLink, HlmAlertImports, HlmButton, HlmCardImports, HlmSpinner, AuthLayout],
  template: `
    <app-auth-layout title="Confirming your email.">
      <section hlmCard>
        <div hlmCardContent>
          @switch (state()) {
            @case ('working') {
              <div class="flex justify-center py-4"><hlm-spinner aria-label="Confirming" /></div>
            }
            @case ('done') {
              <div hlmAlert>
                <h4 hlmAlertTitle>Email confirmed</h4>
                <p hlmAlertDescription>
                  Your address is verified. You can now reset your password with it if you ever need
                  to.
                </p>
              </div>
            }
            @case ('failed') {
              <div hlmAlert variant="destructive">
                <h4 hlmAlertTitle>This link didn't work</h4>
                <p hlmAlertDescription>
                  It may have expired or been used already. Request a new one in your settings.
                </p>
              </div>
            }
          }
        </div>
      </section>
      <a hlmBtn variant="link" [routerLink]="signedIn() ? '/settings' : '/login'">{{
        signedIn() ? 'Go to settings' : 'Sign in'
      }}</a>
    </app-auth-layout>
  `,
})
export class VerifyEmail implements OnInit {
  readonly token = input<string>();

  private readonly api = inject(AuthApi);
  private readonly auth = inject(AuthStore);
  protected readonly signedIn = computed(() => this.auth.status() === 'signedIn');
  protected readonly state = signal<'working' | 'done' | 'failed'>('working');

  async ngOnInit(): Promise<void> {
    try {
      await firstValueFrom(this.api.verifyEmail(this.token() ?? ''));
      this.state.set('done');
    } catch {
      this.state.set('failed');
    }
  }
}
