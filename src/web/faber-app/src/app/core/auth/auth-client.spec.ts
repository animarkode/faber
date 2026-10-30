import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { AuthClient } from './auth-client';
import { AuthStore } from './auth-store';
import { API_ROUTES } from '../routes/api-routes';

describe('AuthClient', () => {
  let client: AuthClient;
  let httpMock: HttpTestingController;
  let store: { clearUser: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    store = { clearUser: vi.fn() };
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AuthStore, useValue: store }],
    });
    client = TestBed.inject(AuthClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should end the session on the server, then forget the user', () => {
    let done = false;
    client.signOut().subscribe(() => (done = true));

    const req = httpMock.expectOne(API_ROUTES.auth.logout);
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(done).toBe(true);
    expect(store.clearUser).toHaveBeenCalled();
  });

  it('should forget the user even when the server call fails', () => {
    client.signOut().subscribe({ error: () => undefined });

    httpMock.expectOne(API_ROUTES.auth.logout).flush(null, { status: 500, statusText: 'Error' });

    expect(store.clearUser).toHaveBeenCalled();
  });
});
