import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { AuthStore } from './auth-store';
import { API_ROUTES } from '../routes/api-routes';

// Built from a JSON string, exactly as the API serializes UserResponse, so the
// frontend contract's spelling cannot leak into the fixture.
function userPayload(): object {
  return JSON.parse(
    '{"id":"user-1","email":"john@example.com","firstName":"John","lastName":"Doe","name":"John Doe",' +
      '"roles":["regular"]}',
  );
}

describe('AuthStore', () => {
  let service: AuthStore;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  async function settle(): Promise<void> {
    await Promise.resolve();
    TestBed.tick();
  }

  it('should ask the API who is signed in, relying on the session cookie alone', async () => {
    TestBed.tick();

    const req = httpMock.expectOne(API_ROUTES.auth.user);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush(userPayload());
    await settle();

    expect(service.authenticated()).toBe(true);
    expect(service.user()?.email).toBe('john@example.com');
  });

  it('should treat a 401 as signed out', async () => {
    TestBed.tick();
    httpMock.expectOne(API_ROUTES.auth.user).flush(null, { status: 401, statusText: 'Unauthorized' });
    await settle();

    expect(service.authenticated()).toBe(false);
    expect(service.user()).toBeNull();
  });

  it('should drop the user without issuing another request on clearUser', async () => {
    TestBed.tick();
    httpMock.expectOne(API_ROUTES.auth.user).flush(userPayload());
    await settle();

    service.clearUser();
    await settle();

    expect(service.authenticated()).toBe(false);
    httpMock.expectNone(API_ROUTES.auth.user);
  });

  it('should ask again on reload', async () => {
    TestBed.tick();
    httpMock.expectOne(API_ROUTES.auth.user).flush(null, { status: 401, statusText: 'Unauthorized' });
    await settle();

    service.reload();
    TestBed.tick();
    httpMock.expectOne(API_ROUTES.auth.user).flush(userPayload());
    await settle();

    expect(service.authenticated()).toBe(true);
  });
});
