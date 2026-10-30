import { DOCUMENT, inject, Injectable } from '@angular/core';
import { API_ROUTES } from '../routes/api-routes';

/**
 * Leaves the app for Keycloak. Signing in and registering are full-page navigations through the API,
 * never fetches: Keycloak's pages are where credentials are typed, and the browser must carry the
 * redirects and cookies of that flow itself.
 */
@Injectable({
  providedIn: 'root',
})
export class AuthRedirect {
  private readonly document = inject(DOCUMENT);

  signIn(returnUrl = '/'): void {
    this.document.location.assign(API_ROUTES.auth.login(returnUrl));
  }

  signUp(): void {
    this.document.location.assign(API_ROUTES.auth.register);
  }
}
