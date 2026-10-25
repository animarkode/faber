import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthClient } from '../auth-client';
import { APP_ROUTES } from '../../../core/routes/app-routes';
import { FbInput } from '../../../shared/components/fb-input/fb-input';
import { FbAuthCard } from '../../../shared/components/fb-auth-card/fb-auth-card';
import { FbButton } from '../../../shared/components/fb-button/fb-button';
import { ExternalSignInErrorStore } from '../external-sign-in/external-sign-in-error-store';
import { ExternalProvidersStore } from '../external-sign-in/external-providers-store';
import { SocialSignIn } from '../external-sign-in/social-sign-in/social-sign-in';
import { FbToast } from '../../../shared/components/fb-toast/fb-toast';

@Component({
  selector: 'app-sign-in',
  imports: [ReactiveFormsModule, RouterLink, FbInput, FbAuthCard, FbButton, FbToast, SocialSignIn],
  templateUrl: './sign-in.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex w-full justify-center' },
})
export class SignIn implements OnInit {
  private readonly router = inject(Router);
  private readonly externalSignInErrors = inject(ExternalSignInErrorStore);
  private readonly authClient = inject(AuthClient);
  private readonly fb = inject(NonNullableFormBuilder);
  /** False until the providers request finishes; the page holds its content until then. */
  protected readonly providersSettled = inject(ExternalProvidersStore).settled;

  readonly form = this.fb.group({
    userName: ['', Validators.required],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  readonly loading = signal(false);
  readonly invalidLogin = signal(false);
  readonly externalError = signal<string | null>(null);
  readonly toastMessage = computed(() =>
    this.invalidLogin() ? 'Wrong username or password.' : this.externalError(),
  );

  ngOnInit(): void {
    this.showExternalError();
  }

  dismissToast(): void {
    this.invalidLogin.set(false);
    this.externalError.set(null);
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.externalError.set(null);
    this.loading.set(true);
    this.authClient
      .signIn(this.form.getRawValue())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => this.router.navigate([APP_ROUTES.home]),
        error: () => this.invalidLogin.set(true),
      });
  }

  private showExternalError(): void {
    this.externalError.set(this.externalSignInErrors.consume());
  }

  protected readonly APP_ROUTES = APP_ROUTES;
}
