import { DOCUMENT, inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, httpResource } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_ROUTES } from '../../../core/routes/api-routes';
import {
  AccountAction,
  AccountDevice,
  AccountProfile,
  CredentialGroup,
  LinkedAccount,
  UpdateProfileRequest,
} from './account-contracts';

/**
 * The account settings the API manages on the person's behalf in Keycloak. Reads are resources (create them
 * in an injection context); changes are observables; actions that need Keycloak's own pages are full-page
 * navigations, never fetches, as sign-in is.
 */
@Injectable({ providedIn: 'root' })
export class AccountClient {
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);

  profileResource() {
    return httpResource<AccountProfile>(() => API_ROUTES.auth.account.profile);
  }

  credentialsResource() {
    return httpResource<CredentialGroup[]>(() => API_ROUTES.auth.account.credentials);
  }

  devicesResource() {
    return httpResource<AccountDevice[]>(() => API_ROUTES.auth.account.devices);
  }

  linkedAccountsResource() {
    return httpResource<LinkedAccount[]>(() => API_ROUTES.auth.account.linkedAccounts);
  }

  updateProfile(request: UpdateProfileRequest): Observable<AccountProfile> {
    return this.http.post<AccountProfile>(API_ROUTES.auth.account.profile, request);
  }

  removeCredential(id: string): Observable<void> {
    return this.http.delete<void>(API_ROUTES.auth.account.credential(id));
  }

  unlinkAccount(alias: string): Observable<void> {
    return this.http.delete<void>(API_ROUTES.auth.account.linkedAccount(alias));
  }

  signOutSession(id: string): Observable<void> {
    return this.http.delete<void>(API_ROUTES.auth.account.session(id));
  }

  signOutOtherSessions(): Observable<void> {
    return this.http.delete<void>(API_ROUTES.auth.account.otherSessions);
  }

  /** Leaves the app for the Keycloak flow behind the action. */
  startAction(action: AccountAction): void {
    this.document.location.assign(API_ROUTES.auth.account.action(action));
  }
}

/** The human message of a failed account call: the problem's `detail` when the API gave one. */
export function problemDetail(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse) {
    const detail = (error.error as { detail?: unknown } | null)?.detail;
    if (typeof detail === 'string' && detail.length > 0) return detail;
  }
  return fallback;
}
