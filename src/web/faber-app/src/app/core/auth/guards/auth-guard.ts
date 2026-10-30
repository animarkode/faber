import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { toObservable } from '@angular/core/rxjs-interop';
import { filter, map, take } from 'rxjs';
import { AuthStore } from '../auth-store';
import { AuthRedirect } from '../auth-redirect';

/** Lets signed-in users through; sends everyone else to Keycloak's sign-in and back to the same page. */
export const authGuard: CanActivateFn = (_route, state) => {
  const authStore = inject(AuthStore);
  const redirect = inject(AuthRedirect);

  if (authStore.authenticated()) {
    return true;
  }

  return toObservable(authStore.loading).pipe(
    filter((loading) => !loading),
    take(1),
    map(() => {
      if (authStore.authenticated()) {
        return true;
      }

      redirect.signIn(state.url);
      return false;
    }),
  );
};
