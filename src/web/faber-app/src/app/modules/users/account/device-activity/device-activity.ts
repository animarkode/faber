import {
  ChangeDetectionStrategy,
  Component,
  computed,
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
import { AccountDevice } from '../account-contracts';
import { FbButton } from '../../../../shared/components/fb-button/fb-button';

@Component({
  selector: 'app-device-activity',
  imports: [DatePipe, FbButton],
  templateUrl: './device-activity.html',
  styleUrl: './device-activity.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeviceActivity {
  private readonly client = inject(AccountClient);
  private readonly confirmation = inject(AccountConfirm);
  private readonly destroyRef = inject(DestroyRef);

  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');

  protected readonly devices = this.client.devicesResource();
  protected readonly error = signal<string | null>(null);

  protected readonly hasOthers = computed(() => (this.devices.value() ?? []).some(d => !d.current));

  protected title(device: AccountDevice): string {
    const system = [device.os, device.osVersion].filter(Boolean).join(' ');
    return [device.device, system, device.browser].filter(Boolean).join(' · ') || 'Unknown device';
  }

  protected signOut(device: AccountDevice): void {
    this.confirm(
      { title: 'Sign out device', message: `Sign out of ${this.title(device)}?`, confirmLabel: 'Sign out' },
      () => this.client.signOutSession(device.id),
    );
  }

  protected signOutOthers(): void {
    this.confirm(
      {
        title: 'Sign out all other devices',
        message: 'You will be signed out everywhere except this device.',
        confirmLabel: 'Sign out all',
      },
      () => this.client.signOutOtherSessions(),
    );
  }

  private confirm(
    data: { title: string; message: string; confirmLabel: string },
    call: () => ReturnType<AccountClient['signOutSession']>,
  ): void {
    this.error.set(null);
    this.confirmation
      .ask(data)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(confirmed => {
        if (!confirmed) return;
        call()
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe({
            next: () => {
              this.devices.reload();
              this.heading()?.nativeElement.focus();
            },
            error: (e: unknown) => this.error.set(problemDetail(e, 'The device could not be signed out.')),
          });
      });
  }
}
