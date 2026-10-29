import { computed, Injectable, signal } from '@angular/core';
import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { User } from './contracts/user';
import { API_ROUTES } from '../routes/api-routes';

/**
 * Who is signed in. The session lives on the server behind an HttpOnly cookie, so the only way to know is
 * to ask the API; there is no token to read or keep.
 */
@Injectable({
  providedIn: 'root',
})
export class AuthStore {
  /**
   * The only signal the request computation reads, so nothing but an explicit `reload()` re-runs it and a
   * resolved user is never silently discarded (issue #426).
   */
  private readonly sessionEpoch = signal(0);

  private readonly userResource = httpResource<User>(() => {
    this.sessionEpoch();
    return API_ROUTES.auth.user;
  });

  readonly user = computed(() => {
    if (this.userResource.status() === 'error') return null;
    return this.userResource.value() ?? null;
  });
  readonly authenticated = computed(() => !!this.user());
  readonly loading = this.userResource.isLoading;

  readonly error = computed(() => {
    const err = this.userResource.error();
    if (!err) return null;
    return err instanceof HttpErrorResponse ? err.message : 'Error getting user.';
  });

  /** Forgets the user locally after the session has been ended on the server. */
  clearUser(): void {
    this.userResource.set(undefined);
  }

  /** Asks the API again, from any resource status. */
  reload(): void {
    this.sessionEpoch.update((epoch) => epoch + 1);
  }
}
