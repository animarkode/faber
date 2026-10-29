import { TestBed } from '@angular/core/testing';
import { DOCUMENT } from '@angular/common';

import { AuthRedirect } from './auth-redirect';
import { API_ROUTES } from '../routes/api-routes';

describe('AuthRedirect', () => {
  let assign: ReturnType<typeof vi.fn>;
  let redirect: AuthRedirect;

  beforeEach(() => {
    assign = vi.fn();
    TestBed.configureTestingModule({
      providers: [{ provide: DOCUMENT, useValue: { location: { assign } } }],
    });
    redirect = TestBed.inject(AuthRedirect);
  });

  it('should navigate the whole page to the API sign-in, carrying the return path', () => {
    redirect.signIn('/resumes/42');

    expect(assign).toHaveBeenCalledWith(API_ROUTES.auth.login('/resumes/42'));
    expect(API_ROUTES.auth.login('/resumes/42')).toContain('returnUrl=%2Fresumes%2F42');
  });

  it('should navigate the whole page to Keycloak registration', () => {
    redirect.signUp();

    expect(assign).toHaveBeenCalledWith(API_ROUTES.auth.register);
  });
});
