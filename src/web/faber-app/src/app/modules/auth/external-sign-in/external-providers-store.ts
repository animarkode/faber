import { computed, Injectable } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { API_ROUTES } from '../../../core/routes/api-routes';
import { ExternalProvidersResponse } from './external-providers-response';

/**
 * The provider aliases the API offers, loaded once per app session so that
 * moving between sign-in and sign-up neither refetches nor makes the buttons
 * pop in again.
 */
@Injectable({
  providedIn: 'root',
})
export class ExternalProvidersStore {
  private readonly providersResource = httpResource<ExternalProvidersResponse>(
    () => API_ROUTES.auth.external.providers,
  );

  /** Reported aliases; empty while loading or when the request failed. */
  readonly providers = computed<string[]>(() =>
    this.providersResource.status() === 'resolved' ? (this.providersResource.value()?.providers ?? []) : [],
  );

  /** True once the request has finished, whether it succeeded or failed. */
  readonly settled = computed(() => {
    const status = this.providersResource.status();
    return status === 'resolved' || status === 'error';
  });
}
