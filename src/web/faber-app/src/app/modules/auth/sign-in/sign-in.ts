import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { LUCIDE_ICONS, LucideAngularModule, LucideIconProvider, X } from 'lucide-angular';
import { AuthClient } from '../auth-client';
import { APP_ROUTES } from '../../../core/routes/app-routes';
import { FbInput } from '../../../shared/components/fb-input/fb-input';
import { FbAuthCard } from '../../../shared/components/fb-auth-card/fb-auth-card';
import { FbButton } from '../../../shared/components/fb-button/fb-button';
import { FbIconButton } from '../../../shared/components/fb-icon-button/fb-icon-button';
import { SocialSignIn } from '../external-sign-in/social-sign-in/social-sign-in';
import { FbToast } from '../../../shared/components/fb-toast/fb-toast';
import { FbThemeToggle } from '../../../shared/components/fb-theme-toggle/fb-theme-toggle';

@Component({
  selector: 'app-sign-in',
  imports: [ReactiveFormsModule, RouterLink, FbInput, FbAuthCard, FbButton, FbIconButton, FbThemeToggle, FbToast, LucideAngularModule, SocialSignIn],
  providers: [{ provide: LUCIDE_ICONS, multi: true, useValue: new LucideIconProvider({ X }) }],
  templateUrl: './sign-in.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex w-full justify-center' },
})
export class SignIn {
  private readonly router = inject(Router);
  private readonly authClient = inject(AuthClient);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly form = this.fb.group({
    userName: ['', Validators.required],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  readonly loading = signal(false);
  readonly invalidLogin = signal(false);

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.authClient
      .signIn(this.form.getRawValue())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => this.router.navigate([APP_ROUTES.home]),
        error: () => this.invalidLogin.set(true),
      });
  }

  protected readonly APP_ROUTES = APP_ROUTES;
}
