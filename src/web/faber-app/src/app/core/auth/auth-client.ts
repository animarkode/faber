import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { finalize, Observable } from 'rxjs';
import { AuthStore } from './auth-store';
import { API_ROUTES } from '../routes/api-routes';

@Injectable({
  providedIn: 'root',
})
export class AuthClient {
  private readonly http = inject(HttpClient);
  private readonly authStore = inject(AuthStore);

  /**
   * Ends the session on the server (the API also ends the Keycloak session), then forgets the user here.
   * The user is forgotten even if the call fails, so the app never keeps showing a signed-in state.
   */
  signOut(): Observable<void> {
    return this.http
      .post<void>(API_ROUTES.auth.logout, null)
      .pipe(finalize(() => this.authStore.clearUser()));
  }
}
