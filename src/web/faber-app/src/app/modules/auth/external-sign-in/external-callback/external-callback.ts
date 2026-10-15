import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  Injector,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { filter, take, timer } from 'rxjs';
import { AuthStore } from '../../../../core/auth/auth-store';
import { APP_ROUTES } from '../../../../core/routes/app-routes';
import { FbAuthCard } from '../../../../shared/components/fb-auth-card/fb-auth-card';
import { EXTERNAL_SIGN_IN_SESSION_FAILED } from '../external-sign-in-errors';

const ANNOUNCE_DELAY_MS = 150;

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
  private readonly authStore = inject(AuthStore);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);

  /**
   * Starts empty and is filled a moment later: a live region announces changes,
   * not the text it is inserted with, and a same-tick update can be merged
   * into the insertion.
   */
  protected readonly status = signal('');

  ngOnInit(): void {
    this.announceProgress();
    this.completeSignIn();
  }

  private announceProgress(): void {
    timer(ANNOUNCE_DELAY_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.status.set('Signing you in…'));
  }

  /**
   * The API has just set the refresh cookie, so this is a hard reload with a
   * session: `AuthStore`'s `/auth/me` gets a 401 and the auth interceptor
   * refreshes once. Waiting on the store (instead of refreshing here too) keeps
   * it to a single refresh-token exchange.
   */
  private completeSignIn(): void {
    if (!this.authStore.loading() && !this.authStore.authenticated()) {
      this.authStore.reload();
    }

    toObservable(this.authStore.loading, { injector: this.injector })
      .pipe(
        filter((loading) => !loading),
        take(1),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => this.leave());
  }

  private leave(): void {
    if (this.authStore.authenticated()) {
      this.router.navigate([APP_ROUTES.home], { replaceUrl: true });
      return;
    }

    this.router.navigate([APP_ROUTES.auth.signIn], {
      queryParams: { error: EXTERNAL_SIGN_IN_SESSION_FAILED },
      replaceUrl: true,
    });
  }
}
