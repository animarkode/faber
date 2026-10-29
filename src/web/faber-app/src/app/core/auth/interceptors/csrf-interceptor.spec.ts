import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { csrfInterceptor } from './csrf-interceptor';
import { AuthStore } from '../auth-store';
import { API_ROUTES } from '../../routes/api-routes';

describe('csrfInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let store: { reload: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    store = { reload: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([csrfInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthStore, useValue: store },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should mark calls to the API with the CSRF header', () => {
    http.get(API_ROUTES.resumes.root).subscribe();

    expect(httpMock.expectOne(API_ROUTES.resumes.root).request.headers.get('X-CSRF')).toBe('1');
  });

  it('should not send the header to another origin', () => {
    http.get('https://elsewhere.example/data').subscribe();

    expect(httpMock.expectOne('https://elsewhere.example/data').request.headers.has('X-CSRF')).toBe(false);
  });

  it('should re-check the session when an API call says the session is gone', () => {
    http.get(API_ROUTES.resumes.root).subscribe({ error: () => undefined });

    httpMock.expectOne(API_ROUTES.resumes.root).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(store.reload).toHaveBeenCalledTimes(1);
  });

  it('should not loop when the session check itself answers 401', () => {
    http.get(API_ROUTES.auth.user).subscribe({ error: () => undefined });

    httpMock.expectOne(API_ROUTES.auth.user).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(store.reload).not.toHaveBeenCalled();
  });
});
