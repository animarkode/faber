import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { signal } from '@angular/core';
import { lastValueFrom, Observable } from 'rxjs';

import { authGuard } from './auth-guard';
import { AuthStore } from '../auth-store';
import { AuthRedirect } from '../auth-redirect';

describe('authGuard', () => {
  let mockAuthStore: {
    authenticated: ReturnType<typeof signal<boolean>>;
    loading: ReturnType<typeof signal<boolean>>;
  };
  let redirect: { signIn: ReturnType<typeof vi.fn> };
  const state = { url: '/resumes/42' } as RouterStateSnapshot;

  beforeEach(() => {
    mockAuthStore = { authenticated: signal(false), loading: signal(false) };
    redirect = { signIn: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthStore, useValue: mockAuthStore },
        { provide: AuthRedirect, useValue: redirect },
      ],
    });
  });

  function run(): boolean | Observable<boolean> {
    return TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, state),
    ) as boolean | Observable<boolean>;
  }

  it('should let a signed-in user through', () => {
    mockAuthStore.authenticated.set(true);

    expect(run()).toBe(true);
    expect(redirect.signIn).not.toHaveBeenCalled();
  });

  it('should send a signed-out user to the sign-in page and back to the same page', async () => {
    const result = run();
    const value = result instanceof Observable ? await lastValueFrom(result) : result;

    expect(value).toBe(false);
    expect(redirect.signIn).toHaveBeenCalledWith('/resumes/42');
  });

  it('should wait for the session check to finish', async () => {
    mockAuthStore.loading.set(true);
    const result = run();
    expect(result).not.toBe(true);

    mockAuthStore.authenticated.set(true);
    mockAuthStore.loading.set(false);

    expect(await lastValueFrom(result as Observable<boolean>)).toBe(true);
    expect(redirect.signIn).not.toHaveBeenCalled();
  });
});
