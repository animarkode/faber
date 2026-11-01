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
import { DatePipe } from '@angular/common';
import { AccountClient, problemDetail } from '../account-client';
import { AccountConfirm } from '../account-confirm';
import { AccountAction, CredentialGroup, StoredCredential } from '../account-contracts';
import { humanize } from '../account-labels';
import { FbButton } from '../../../../shared/components/fb-button/fb-button';

@Component({
  selector: 'app-signing-in',
  imports: [DatePipe, FbButton],
  templateUrl: './signing-in.html',
  styleUrl: './signing-in.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SigningIn {
  private readonly client = inject(AccountClient);
  private readonly confirmation = inject(AccountConfirm);
  private readonly destroyRef = inject(DestroyRef);

  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');

  protected readonly credentials = this.client.credentialsResource();
  protected readonly error = signal<string | null>(null);

  protected typeName(group: CredentialGroup): string {
    return humanize(group.displayName) || humanize(group.type);
  }

  protected itemName(group: CredentialGroup, item: StoredCredential): string {
    return item.userLabel || this.typeName(group);
  }

  protected removeLabel(group: CredentialGroup, item: StoredCredential): string {
    return `Remove ${this.typeName(group).toLowerCase()} '${this.itemName(group, item)}'`;
  }

  protected start(action: AccountAction): void {
    this.client.startAction(action);
  }

  protected remove(group: CredentialGroup, item: StoredCredential): void {
    this.error.set(null);
    this.confirmation
      .ask({
        title: `Remove ${this.typeName(group).toLowerCase()}`,
        message: `Remove '${this.itemName(group, item)}'? You will no longer be able to sign in with it.`,
        confirmLabel: 'Remove',
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(confirmed => {
        if (confirmed) this.delete(item);
      });
  }

  private delete(item: StoredCredential): void {
    this.client
      .removeCredential(item.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.credentials.reload();
          this.heading()?.nativeElement.focus();
        },
        error: (e: unknown) => this.error.set(problemDetail(e, 'The sign-in method could not be removed.')),
      });
  }
}
