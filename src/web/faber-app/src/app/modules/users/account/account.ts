import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { AuthStore } from '../../../core/auth/auth-store';
import { FbButton } from '../../../shared/components/fb-button/fb-button';

/**
 * Shows who is signed in. Everything about the account itself — name, e-mail, password, passkeys, 2FA,
 * linked Google/GitHub sign-ins, open sessions — is managed in Keycloak's account console, so the API never
 * needs the right to change users.
 */
@Component({
  selector: 'app-account',
  imports: [FbButton],
  templateUrl: './account.html',
  styleUrl: './account.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Account {
  private readonly authStore = inject(AuthStore);

  readonly user = this.authStore.user;
  readonly displayName = computed(() => {
    const user = this.user();
    return user?.name ?? [user?.firstName, user?.lastName].filter(Boolean).join(' ');
  });
}
