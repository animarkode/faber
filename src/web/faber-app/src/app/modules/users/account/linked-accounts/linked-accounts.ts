import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AccountClient, problemDetail } from '../account-client';
import { AccountConfirm } from '../account-confirm';
import { LinkedAccount } from '../account-contracts';
import { FbButton } from '../../../../shared/components/fb-button/fb-button';

@Component({
  selector: 'app-linked-accounts',
  imports: [FbButton],
  templateUrl: './linked-accounts.html',
  styleUrl: './linked-accounts.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LinkedAccounts {
  private readonly client = inject(AccountClient);
  private readonly confirmation = inject(AccountConfirm);
  private readonly destroyRef = inject(DestroyRef);

  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');

  protected readonly accounts = this.client.linkedAccountsResource();
  protected readonly error = signal<string | null>(null);

  protected link(account: LinkedAccount): void {
    if (account.linkAction) this.client.startAction(account.linkAction);
  }

  protected unlink(account: LinkedAccount): void {
    this.error.set(null);
    this.confirmation
      .ask({
        title: `Unlink ${account.displayName}`,
        message: `You will no longer be able to sign in with ${account.displayName}.`,
        confirmLabel: 'Unlink',
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(confirmed => {
        if (confirmed) this.delete(account);
      });
  }

  private delete(account: LinkedAccount): void {
    this.client
      .unlinkAccount(account.providerAlias)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.accounts.reload();
          this.heading()?.nativeElement.focus();
        },
        error: (e: unknown) => this.error.set(problemDetail(e, `${account.displayName} could not be unlinked.`)),
      });
  }
}
