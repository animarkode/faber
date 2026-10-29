import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  Injector,
  signal,
  viewChild,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthClient } from '../auth-client';
import { APP_ROUTES } from '../../../core/routes/app-routes';
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
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly injector = inject(Injector);
  private readonly heading = viewChild.required<ElementRef<HTMLHeadingElement>>('heading');

  readonly form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
  });

  readonly loading = signal(false);
  readonly sent = signal(false);
  readonly error = signal(false);

  /** Once the link is sent the form gives way to a confirmation under its own title. */
  protected readonly title = computed(() => (this.sent() ? 'Check your email' : 'Forgot password?'));

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(false);

    this.authClient
      .forgotPassword(this.form.controls.email.value)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => this.showConfirmation(),
        error: () => this.error.set(true),
      });
  }

  continueToSignIn(): void {
    this.router.navigate([APP_ROUTES.auth.signIn]);
  }

  /** The submit button disappears with the form, so focus moves to the new title. */
  private showConfirmation(): void {
    this.sent.set(true);
    afterNextRender(() => this.heading().nativeElement.focus(), { injector: this.injector });
  }
}
