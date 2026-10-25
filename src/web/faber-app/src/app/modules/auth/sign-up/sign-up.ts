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
import type { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AtSign, LUCIDE_ICONS, LucideAngularModule, LucideIconProvider } from 'lucide-angular';
import { AuthClient } from '../auth-client';
import { APP_ROUTES } from '../../../core/routes/app-routes';
import { passwordMatchValidator } from '../shared/validators/password-match.validator';
import { FbInput } from '../../../shared/components/fb-input/fb-input';
import { FbAuthCard } from '../../../shared/components/fb-auth-card/fb-auth-card';
import { FbButton } from '../../../shared/components/fb-button/fb-button';
import { SocialSignIn } from '../external-sign-in/social-sign-in/social-sign-in';
import { EmailFormChoice } from '../shared/email-form-choice';
import { FbToast } from '../../../shared/components/fb-toast/fb-toast';

@Component({
  selector: 'app-sign-up',
  imports: [ReactiveFormsModule, RouterLink, FbInput, FbAuthCard, FbButton, FbToast, LucideAngularModule, SocialSignIn],
  providers: [
    EmailFormChoice,
    { provide: LUCIDE_ICONS, multi: true, useValue: new LucideIconProvider({ AtSign }) },
  ],
  templateUrl: './sign-up.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex w-full justify-center' },
})
export class SignUp {
  private readonly router = inject(Router);
  private readonly authClient = inject(AuthClient);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly injector = inject(Injector);
  private readonly heading = viewChild.required<ElementRef<HTMLHeadingElement>>('heading');
  protected readonly choice = inject(EmailFormChoice);

  readonly form = this.fb.group(
    {
      userName: ['', Validators.required],
      firstName: ['', Validators.required],
      lastName: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required, Validators.minLength(8)]],
    },
    { validators: passwordMatchValidator('password', 'confirmPassword') },
  );

  readonly loading = signal(false);
  readonly invalidRegister = signal(false);
  readonly errors = signal<string[]>([]);
  /** Set once the account is created; sign-in waits for the e-mail to be verified. */
  readonly registered = signal(false);

  /** Once registered the form gives way to a reminder to verify the e-mail under its own title. */
  protected readonly title = computed(() => (this.registered() ? 'Check your email' : 'Sign up'));

  readonly errorMessage = computed(() => {
    const list = this.errors();
    if (list.length) return list.join(', ');
    return this.invalidRegister() ? 'Sign-up failed. Try again.' : null;
  });

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.authClient
      .signUp(this.form.getRawValue())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => this.showConfirmation(),
        error: (err: HttpErrorResponse) => {
          this.invalidRegister.set(true);
          const messages = err.error?.errors?.map((e: { errorMessage: string }) => e.errorMessage) ?? [];
          this.errors.set(messages);
        },
      });
  }

  continueToSignIn(): void {
    this.router.navigate([APP_ROUTES.auth.signIn]);
  }

  /** The submit button disappears with the form, so focus moves to the new title. */
  private showConfirmation(): void {
    this.registered.set(true);
    afterNextRender(() => this.heading().nativeElement.focus(), { injector: this.injector });
  }

  protected readonly APP_ROUTES = APP_ROUTES;
}
