import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { LUCIDE_ICONS, LucideAngularModule, LucideIconProvider, X } from 'lucide-angular';
import { AuthClient } from '../auth-client';
import { APP_ROUTES } from '../../../core/routes/app-routes';
import { FbInput } from '../../../shared/components/fb-input/fb-input';
import { FbAuthCard } from '../../../shared/components/fb-auth-card/fb-auth-card';
import { FbButton } from '../../../shared/components/fb-button/fb-button';
import { FbIconButton } from '../../../shared/components/fb-icon-button/fb-icon-button';
import { externalSignInErrorMessage } from '../external-sign-in/external-sign-in-errors';
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
export class SignIn implements OnInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly authClient = inject(AuthClient);
  private readonly fb = inject(NonNullableFormBuilder);

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
    const code = this.route.snapshot.queryParamMap.get('error');
    if (!code) {
      return;
    }
    this.externalError.set(externalSignInErrorMessage(code));
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { error: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  protected readonly APP_ROUTES = APP_ROUTES;
}
