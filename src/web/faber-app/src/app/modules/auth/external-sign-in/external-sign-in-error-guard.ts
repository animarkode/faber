import { inject } from '@angular/core';
import { CanActivateFn, RedirectCommand, Router } from '@angular/router';
import { externalSignInErrorMessage } from './external-sign-in-errors';
import { ExternalSignInErrorStore } from './external-sign-in-error-store';

/**
 * Turns the API's `?error=<code>` failure redirect into a readable message for
 * the sign-in page, and redirects to the same URL without `error` before the
 * page activates — the router's own redirect, rather than a second navigation
 * started from inside the component.
 */
export const externalSignInErrorGuard: CanActivateFn = (route, state) => {
  const code = route.queryParamMap.get('error');
  if (!code) {
    return true;
  }

  inject(ExternalSignInErrorStore).set(externalSignInErrorMessage(code));

  const router = inject(Router);
  const target = router.parseUrl(state.url);
  delete target.queryParams['error'];

  return new RedirectCommand(target, { replaceUrl: true });
};
