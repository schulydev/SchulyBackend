import { Component, inject, signal } from '@angular/core';
import { email, form, FormField, FormRoot, required } from '@angular/forms/signals';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideImageUp, lucideX } from '@ng-icons/lucide';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmAvatarImports } from '@spartan-ng/helm/avatar';
import { HlmBadge } from '@spartan-ng/helm/badge';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmSpinner } from '@spartan-ng/helm/spinner';
import { SecurityStore } from '../../stores/security.store';
import { UserStore } from '../../stores/user.store';

@Component({
  selector: 'app-account-card',
  imports: [
    FormRoot,
    FormField,
    NgIcon,
    HlmAlertImports,
    HlmAvatarImports,
    HlmBadge,
    HlmButton,
    HlmCardImports,
    HlmFieldImports,
    HlmInput,
    HlmSpinner,
  ],
  providers: [provideIcons({ lucideImageUp, lucideX })],
  template: `
    <section hlmCard>
      <div hlmCardHeader>
        <h3 hlmCardTitle>Account</h3>
        <p hlmCardDescription>Your profile picture and sign-in email.</p>
      </div>
      <div hlmCardContent class="flex flex-col gap-6">
        <div class="flex flex-wrap items-center gap-4">
          <hlm-avatar class="size-16 rounded-xl">
            @if (user.avatar(); as src) {
              <img hlmAvatarImage [src]="src" [alt]="user.name()" />
            }
            <span hlmAvatarFallback class="rounded-xl text-lg">{{ user.initials() }}</span>
          </hlm-avatar>
          <div class="grid flex-1 gap-0.5">
            <span class="font-medium">{{ user.name() }}</span>
            <span class="text-muted-foreground text-xs"
              >{{ user.schoolCount() }} linked
              {{ user.schoolCount() === 1 ? 'school' : 'schools' }}</span
            >
          </div>
          <div class="flex gap-2">
            <input
              #file
              type="file"
              accept="image/png,image/jpeg,image/webp"
              class="hidden"
              (change)="upload(file)"
            />
            <button
              hlmBtn
              variant="outline"
              size="sm"
              [disabled]="user.busy()"
              (click)="file.click()"
            >
              @if (user.busy()) {
                <hlm-spinner />
              } @else {
                <ng-icon name="lucideImageUp" />
              }
              Change picture
            </button>
            @if (user.avatar()) {
              <button
                hlmBtn
                variant="ghost"
                size="sm"
                [disabled]="user.busy()"
                (click)="user.removeAvatar()"
              >
                <ng-icon name="lucideX" />
                Remove
              </button>
            }
          </div>
        </div>

        <div class="flex flex-col gap-3">
          <div class="flex items-center gap-2">
            <span class="text-sm font-medium">Email</span>
            @if (user.email()) {
              <span hlmBadge variant="outline">Confirmed</span>
            } @else {
              <span hlmBadge variant="secondary">Not confirmed</span>
            }
          </div>
          @if (user.email()) {
            <p class="text-muted-foreground text-sm">{{ user.email() }}</p>
          } @else {
            <p class="text-muted-foreground text-sm">
              Confirm your email so you can reset your password if you forget it.
            </p>
            <form
              class="grid gap-3 sm:grid-cols-[1fr_1fr_auto] sm:items-end"
              [formRoot]="verification"
            >
              <div hlmField>
                <label hlmFieldLabel for="verify-email">Email</label>
                <input
                  hlmInput
                  id="verify-email"
                  type="email"
                  autocomplete="email"
                  [formField]="verification.email"
                />
              </div>
              <div hlmField>
                <label hlmFieldLabel for="verify-password">Password</label>
                <input
                  hlmInput
                  id="verify-password"
                  type="password"
                  autocomplete="current-password"
                  [formField]="verification.password"
                />
              </div>
              <button hlmBtn type="submit" [disabled]="security.busy() || verification().invalid()">
                Send link
              </button>
            </form>
          }
        </div>

        @if (picError()) {
          <div hlmAlert variant="destructive">
            <p hlmAlertDescription>{{ picError() }}</p>
          </div>
        }
      </div>
    </section>
  `,
})
export class AccountCard {
  protected readonly user = inject(UserStore);
  protected readonly security = inject(SecurityStore);
  protected readonly picError = signal<string | null>(null);
  private readonly verificationModel = signal({ email: '', password: '' });
  protected readonly verification = form(
    this.verificationModel,
    (path) => {
      required(path.email);
      email(path.email);
      required(path.password);
    },
    { submission: { action: () => this.resend() } },
  );

  protected upload(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (file.size > 2_000_000) {
      this.picError.set('The picture must be at most 2 MB.');
      return;
    }
    this.picError.set(null);
    this.user.uploadAvatar(file);
  }

  private async resend(): Promise<undefined> {
    const { email, password } = this.verificationModel();
    await this.security.resendVerification(email.trim(), password);
    this.verificationModel.update((model) => ({ ...model, password: '' }));
    return undefined;
  }
}
