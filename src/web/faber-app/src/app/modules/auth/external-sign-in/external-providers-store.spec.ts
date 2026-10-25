import { TestBed } from '@angular/core/testing';
import { ApplicationRef } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { ExternalProvidersStore } from './external-providers-store';
import { API_ROUTES } from '../../../core/routes/api-routes';

describe('ExternalProvidersStore', () => {
  let store: ExternalProvidersStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(ExternalProvidersStore);
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(ApplicationRef).tick();
  });

  async function respond(body: object | string, status = 200): Promise<void> {
    http
      .expectOne(API_ROUTES.auth.external.providers)
      .flush(body, { status, statusText: status === 200 ? 'OK' : 'Server Error' });
    await TestBed.inject(ApplicationRef).whenStable();
  }

  it('should not be settled while the request is in flight', () => {
    expect(store.settled()).toBe(false);
    expect(store.providers()).toEqual([]);
  });

  it('should settle with the reported providers', async () => {
    await respond({ providers: ['google'] });

    expect(store.settled()).toBe(true);
    expect(store.providers()).toEqual(['google']);
    expect(store.hasKnownProviders()).toBe(true);
  });

  it('should report no known providers when only unknown aliases come back', async () => {
    await respond({ providers: ['gitlab'] });

    expect(store.settled()).toBe(true);
    expect(store.hasKnownProviders()).toBe(false);
  });

  it('should settle without providers when the request fails', async () => {
    await respond('boom', 500);

    expect(store.settled()).toBe(true);
    expect(store.providers()).toEqual([]);
  });
});
