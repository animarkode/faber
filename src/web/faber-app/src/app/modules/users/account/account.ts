import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';
import { AccountClient } from './account-client';
import { AccountConfirm } from './account-confirm';
import { DeviceActivity } from './device-activity/device-activity';
import { LinkedAccounts } from './linked-accounts/linked-accounts';
import { PersonalInfo } from './personal-info/personal-info';
import { SigningIn } from './signing-in/signing-in';
import { FbButton } from '../../../shared/components/fb-button/fb-button';

const SUCCESS_MESSAGES: Record<string, string> = {
  password: 'Password updated',
  passkey: 'Passkey added',
  totp: 'Authenticator app set up',
  email: 'E-mail updated',
  'link-google': 'Google account linked',
  'link-github': 'GitHub account linked',
};

/**
 * The person's account settings. Everything is edited here; Keycloak is only visited for the steps that
 * need its own pages (password, passkeys, 2FA, e-mail, linking), and returns with `?action=…&status=…`.
 */
@Component({
  selector: 'app-account',
  imports: [FbButton, PersonalInfo, SigningIn, DeviceActivity, LinkedAccounts],
  templateUrl: './account.html',
  styleUrl: './account.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Account {
  private readonly client = inject(AccountClient);
  private readonly confirmation = inject(AccountConfirm);
  private readonly destroyRef = inject(DestroyRef);

  private readonly query = toSignal(inject(ActivatedRoute).queryParamMap.pipe(map(p => ({ action: p.get('action'), status: p.get('status') }))), {
    initialValue: { action: null, status: null },
  });

  protected readonly statusMessage = computed(() => {
    const { action, status } = this.query();
    if (!action || !status) return '';
    if (status === 'success') return SUCCESS_MESSAGES[action] ?? 'Done';
    if (status === 'cancelled') return 'Cancelled';
    return 'Something went wrong';
  });

  protected readonly failed = computed(() => {
    const { action, status } = this.query();
    return !!action && !!status && status !== 'success' && status !== 'cancelled';
  });

  protected deleteAccount(): void {
    this.confirmation
      .ask({
        title: 'Delete account',
        message: 'This permanently deletes your account and your ability to sign in. This cannot be undone.',
        confirmLabel: 'Delete account',
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(confirmed => {
        if (confirmed) this.client.startAction('delete');
      });
  }
}
