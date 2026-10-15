import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { externalSignInErrorGuard } from './external-sign-in-error-guard';
import { externalSignInErrorMessage } from './external-sign-in-errors';
import { ExternalSignInErrorStore } from './external-sign-in-error-store';

@Component({ selector: 'app-sign-in-page-stub', template: '' })
class SignInPageStub {}

describe('externalSignInErrorGuard', () => {
  let harness: RouterTestingHarness;
  let router: Router;
  let store: ExternalSignInErrorStore;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'auth/sign-in', canActivate: [externalSignInErrorGuard], component: SignInPageStub },
        ]),
      ],
    });
    harness = await RouterTestingHarness.create();
    router = TestBed.inject(Router);
    store = TestBed.inject(ExternalSignInErrorStore);
  });

  it('should store the message and drop error from the URL', async () => {
    await harness.navigateByUrl('/auth/sign-in?error=provider_denied');

    expect(router.url).toBe('/auth/sign-in');
    expect(store.consume()).toBe(externalSignInErrorMessage('provider_denied'));
  });

  it('should keep other query params', async () => {
    await harness.navigateByUrl('/auth/sign-in?error=invalid_state&returnUrl=%2Fresumes');

    expect(router.url).toBe('/auth/sign-in?returnUrl=%2Fresumes');
  });

  it('should store the generic message for an unknown code', async () => {
    await harness.navigateByUrl('/auth/sign-in?error=something_new');

    expect(store.consume()).toBe(externalSignInErrorMessage('something_new'));
  });

  it('should let the page activate untouched without an error param', async () => {
    await harness.navigateByUrl('/auth/sign-in');

    expect(router.url).toBe('/auth/sign-in');
    expect(store.consume()).toBeNull();
  });
});
