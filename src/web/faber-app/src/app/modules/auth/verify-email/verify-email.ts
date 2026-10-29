import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { Check, LUCIDE_ICONS, LucideAngularModule, LucideIconProvider, X } from 'lucide-angular';
import { AuthClient } from '../auth-client';
import { APP_ROUTES } from '../../../core/routes/app-routes';
import { FbAuthCard } from '../../../shared/components/fb-auth-card/fb-auth-card';
import { FbButton } from '../../../shared/components/fb-button/fb-button';

@Component({
  selector: 'app-verify-email',
  imports: [FbAuthCard, FbButton, LucideAngularModule],
  providers: [{ provide: LUCIDE_ICONS, multi: true, useValue: new LucideIconProvider({ X, Check }) }],
  templateUrl: './verify-email.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex w-full justify-center' },
})
export class VerifyEmail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authClient = inject(AuthClient);
  private readonly destroyRef = inject(DestroyRef);

  readonly verifying = signal(true);
  readonly verified = signal(false);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    const key = this.route.snapshot.queryParams['key'];

    if (!key) {
      this.error.set('Email or token not provided.');
      this.verifying.set(false);
      return;
    }

    this.authClient.verifyEmail({ combinedKey: { value: key } }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.verified.set(true);
        this.verifying.set(false);
      },
      error: () => {
        this.error.set('Verification failed. The link may have expired.');
        this.verifying.set(false);
      },
    });
  }

  navigateToSignIn(): void {
    this.router.navigate([APP_ROUTES.auth.signIn]);
  }
}
