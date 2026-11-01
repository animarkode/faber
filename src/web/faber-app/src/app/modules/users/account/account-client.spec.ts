import { TestBed } from '@angular/core/testing';
import { DOCUMENT } from '@angular/common';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { AccountClient, problemDetail } from './account-client';
import { API_ROUTES } from '../../../core/routes/api-routes';

describe('AccountClient', () => {
  let client: AccountClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    client = TestBed.inject(AccountClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it.each([
    ['profileResource', API_ROUTES.auth.account.profile],
    ['credentialsResource', API_ROUTES.auth.account.credentials],
    ['devicesResource', API_ROUTES.auth.account.devices],
    ['linkedAccountsResource', API_ROUTES.auth.account.linkedAccounts],
  ] as const)('%s should GET its endpoint', async (method, url) => {
    const resource = TestBed.runInInjectionContext(() => client[method]());
    TestBed.tick();

    const req = http.expectOne(url);
    expect(req.request.method).toBe('GET');
    req.flush([]);
    await Promise.resolve();
    expect(resource.value()).toEqual([]);
  });

  it('updateProfile should POST the names and attributes', () => {
    const body = { firstName: 'Ada', lastName: 'L', attributes: { phone: '1' } };
    client.updateProfile(body).subscribe();

    const req = http.expectOne(API_ROUTES.auth.account.profile);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({});
  });

  it.each([
    ['removeCredential', ['c 1'], '/account/credentials/c%201'],
    ['unlinkAccount', ['google'], '/account/linked-accounts/google'],
    ['signOutSession', ['s1'], '/account/sessions/s1'],
    ['signOutOtherSessions', [], '/account/sessions/others'],
  ] as const)('%s should DELETE %s', (method, args, suffix) => {
    (client[method] as (...a: readonly string[]) => ReturnType<AccountClient['signOutSession']>)(...args).subscribe();

    const req = http.expectOne(r => r.url.endsWith(suffix));
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('problemDetail should prefer the problem detail and fall back otherwise', () => {
    const conflict = new HttpErrorResponse({ status: 409, error: { detail: 'Last method.' } });

    expect(problemDetail(conflict, 'fallback')).toBe('Last method.');
    expect(problemDetail(new HttpErrorResponse({ status: 502 }), 'fallback')).toBe('fallback');
    expect(problemDetail(new Error('x'), 'fallback')).toBe('fallback');
  });
});

describe('AccountClient navigation', () => {
  it('startAction should navigate the whole page to the API action', () => {
    const assign = vi.fn();
    TestBed.configureTestingModule({ providers: [{ provide: DOCUMENT, useValue: { location: { assign } } }] });

    TestBed.inject(AccountClient).startAction('password');

    expect(assign).toHaveBeenCalledWith(API_ROUTES.auth.account.action('password'));
    expect(API_ROUTES.auth.account.action('password')).toMatch(/\/auth\/account-action\/password$/);
  });
});
