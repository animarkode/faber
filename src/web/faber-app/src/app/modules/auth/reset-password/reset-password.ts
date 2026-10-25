import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthClient } from '../auth-client';
import { APP_ROUTES } from '../../../core/routes/app-routes';
import { passwordMatchValidator } from '../shared/validators/password-match.validator';
import { FbInput } from '../../../shared/components/fb-input/fb-input';
import { FbAuthCard } from '../../../shared/components/fb-auth-card/fb-auth-card';
import { FbButton } from '../../../shared/components/fb-button/fb-button';
import { FbToast } from '../../../shared/components/fb-toast/fb-toast';

@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, FbInput, FbAuthCard, FbButton, FbToast],
  templateUrl: './reset-password.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex w-full justify-center' },
})
export class ResetPassword {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly authClient = inject(AuthClient);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly form = this.fb.group(
    {
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmNewPassword: ['', [Validators.required, Validators.minLength(8)]],
    },
    { validators: passwordMatchValidator('newPassword', 'confirmNewPassword') },
  );

  readonly loading = signal(false);
  readonly invalidReset = signal(false);

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    const key = this.route.snapshot.queryParams['key'];

    this.authClient
      .resetPassword({ combinedKey: { value: key }, ...this.form.getRawValue() })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => this.router.navigate([APP_ROUTES.auth.signIn]),
        error: () => this.invalidReset.set(true),
      });
  }
}
