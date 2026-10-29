import { HttpErrorResponse, HttpInterceptorFn, HttpStatusCode } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthStore } from '../auth-store';
import { API_ROUTES } from '../../routes/api-routes';
import { environment } from '../../../../environments/environment';

/** The header the API requires on every cookie-authenticated call; a cross-site page cannot add it. */
const CSRF_HEADER = 'X-CSRF';

/**
 * Marks calls to Faber's own API with the CSRF header, and re-checks the session when the API answers 401
 * (the session ended elsewhere: signed out in another tab, in Keycloak's account console, or expired).
 */
export const csrfInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.api)) {
    return next(req);
  }

  const authStore = inject(AuthStore);

  return next(req.clone({ setHeaders: { [CSRF_HEADER]: '1' } })).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === HttpStatusCode.Unauthorized &&
        req.url !== API_ROUTES.auth.user
      ) {
        authStore.reload();
      }

      return throwError(() => error);
    }),
  );
};
