import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { AuthClient } from '../../auth-client';
import { AuthStore } from '../../../../core/auth/auth-store';
import { APP_ROUTES } from '../../../../core/routes/app-routes';
import { FbAuthCard } from '../../../../shared/components/fb-auth-card/fb-auth-card';
import { EXTERNAL_SIGN_IN_SESSION_FAILED } from '../external-sign-in-errors';

@Component({
  selector: 'app-external-callback',
  imports: [FbAuthCard],
  templateUrl: './external-callback.html',
  styleUrl: './external-callback.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex w-full justify-center' },
})
export class ExternalCallback implements OnInit {
  private readonly router = inject(Router);
  private readonly authClient = inject(AuthClient);
  private readonly authStore = inject(AuthStore);
  private readonly destroyRef = inject(DestroyRef);

  ngOnInit(): void {
    this.completeSignIn();
  }

  private completeSignIn(): void {
    this.authClient
      .refresh()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.enterApp(),
        error: () => this.returnToSignIn(),
      });
  }

  private enterApp(): void {
    this.authStore.reload();
    this.router.navigate([APP_ROUTES.home], { replaceUrl: true });
  }

  private returnToSignIn(): void {
    this.router.navigate([APP_ROUTES.auth.signIn], {
      queryParams: { error: EXTERNAL_SIGN_IN_SESSION_FAILED },
      replaceUrl: true,
    });
  }
}
