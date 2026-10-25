import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AuthClient } from '../auth-client';
import { FbInput } from '../../../shared/components/fb-input/fb-input';
import { FbAuthCard } from '../../../shared/components/fb-auth-card/fb-auth-card';
import { FbButton } from '../../../shared/components/fb-button/fb-button';
import { FbToast } from '../../../shared/components/fb-toast/fb-toast';

@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, FbInput, FbAuthCard, FbButton, FbToast],
  templateUrl: './forgot-password.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex w-full justify-center' },
})
export class ForgotPassword {
  private readonly authClient = inject(AuthClient);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
  });

  readonly loading = signal(false);
  readonly sent = signal(false);
  readonly error = signal(false);

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.sent.set(false);
    this.error.set(false);

    this.authClient
      .forgotPassword(this.form.controls.email.value)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => this.sent.set(true),
        error: () => this.error.set(true),
      });
  }
}
