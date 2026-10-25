import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';

import { SignIn } from './sign-in';
import { AuthClient } from '../auth-client';
import { APP_ROUTES } from '../../../core/routes/app-routes';
import { externalSignInErrorMessage } from '../external-sign-in/external-sign-in-errors';
import { ExternalProvidersStore } from '../external-sign-in/external-providers-store';
import { ExternalSignInErrorStore } from '../external-sign-in/external-sign-in-error-store';
import { expectNoAxeViolations } from '../../../shared/testing/axe';

/** A settled providers store reporting the given aliases. */
function providersStore(providers: string[], settled = true) {
  return {
    providers: signal(providers),
    settled: signal(settled),
  };
}

/** Keeps the page's social buttons from issuing a providers request. */
const noExternalProviders = { provide: ExternalProvidersStore, useValue: providersStore([]) };

describe('SignIn', () => {
  let component: SignIn;
  let fixture: ComponentFixture<SignIn>;
  let router: Router;

  const mockAuthClient = {
    signIn: vi.fn(),
  };

  beforeEach(async () => {
    vi.clearAllMocks();

    await TestBed.configureTestingModule({
      imports: [SignIn],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthClient, useValue: mockAuthClient },
        noExternalProviders,
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SignIn);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have loading false initially', () => {
    expect(component.loading()).toBe(false);
  });

  it('should have invalidLogin false initially', () => {
    expect(component.invalidLogin()).toBe(false);
  });

  it('should not call signIn when form is invalid', () => {
    component.onSubmit();
    expect(mockAuthClient.signIn).not.toHaveBeenCalled();
  });

  it('should navigate to home on successful sign-in', () => {
    mockAuthClient.signIn.mockReturnValue(of({ accessToken: 'token', refreshToken: 'rt' }));

    component.form.setValue({ userName: 'testuser', password: 'password123' });
    component.onSubmit();

    expect(mockAuthClient.signIn).toHaveBeenCalledWith({ userName: 'testuser', password: 'password123' });
    expect(router.navigate).toHaveBeenCalledWith([APP_ROUTES.home]);
  });

  it('should set invalidLogin on error', () => {
    mockAuthClient.signIn.mockReturnValue(throwError(() => new Error('Unauthorized')));

    component.form.setValue({ userName: 'testuser', password: 'password123' });
    component.onSubmit();

    expect(component.invalidLogin()).toBe(true);
  });

  it('should mark all controls as touched when submitting an invalid form', () => {
    component.onSubmit();

    expect(component.form.controls.userName.touched).toBe(true);
    expect(component.form.controls.password.touched).toBe(true);
  });
});

describe('SignIn external sign-in errors', () => {
  const MESSAGE = externalSignInErrorMessage('provider_denied');

  async function create(pending: string | null): Promise<ComponentFixture<SignIn>> {
    await TestBed.configureTestingModule({
      imports: [SignIn],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthClient, useValue: { signIn: vi.fn() } },
        noExternalProviders,
      ],
    }).compileComponents();

    TestBed.inject(ExternalSignInErrorStore).set(pending);
    const fixture = TestBed.createComponent(SignIn);
    await fixture.whenStable();
    return fixture;
  }

  it('should show the message the guard left for it', async () => {
    const fixture = await create(MESSAGE);

    expect(fixture.nativeElement.textContent).toContain(MESSAGE);
  });

  it('should consume the message so it shows only once', async () => {
    await create(MESSAGE);

    expect(TestBed.inject(ExternalSignInErrorStore).consume()).toBeNull();
  });

  it('should show no toast without a pending message', async () => {
    const fixture = await create(null);

    expect(fixture.componentInstance.toastMessage()).toBeNull();
  });

  it('should clear the message when dismissed', async () => {
    const fixture = await create(MESSAGE);

    fixture.componentInstance.dismissToast();

    expect(fixture.componentInstance.toastMessage()).toBeNull();
  });

  it('should have no AXE violations with an error shown', async () => {
    const fixture = await create(MESSAGE);

    await expectNoAxeViolations(fixture);
  }, 20000);
});

describe('SignIn providers and form', () => {
  let fixture: ComponentFixture<SignIn>;

  const root = () => fixture.nativeElement as HTMLElement;
  const form = () => root().querySelector('form');
  const providers = () => root().querySelector('app-social-sign-in');
  const createAccountLink = () =>
    Array.from(root().querySelectorAll('a')).find((a) => a.textContent?.trim() === 'Create account');

  async function render(store: ReturnType<typeof providersStore>): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [SignIn],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthClient, useValue: { signIn: vi.fn() } },
        { provide: ExternalProvidersStore, useValue: store },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SignIn);
    await fixture.whenStable();
  }

  it('should show nothing below the header while providers load', async () => {
    await render(providersStore([], false));

    expect(form()).toBeNull();
    expect(providers()).toBeNull();
  });

  it('should show the form without a provider to offer', async () => {
    await render(providersStore([]));

    expect(form()).not.toBeNull();
  });

  it('should show the providers above the form, with no extra step', async () => {
    await render(providersStore(['google']));

    expect(form()).not.toBeNull();
    expect(providers()!.compareDocumentPosition(form()!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('should keep the create-account link reachable', async () => {
    await render(providersStore(['google']));

    expect(createAccountLink()).toBeDefined();
  });

  it('should have no AXE violations with providers and form', { timeout: 20000 }, async () => {
    await render(providersStore(['google', 'github']));

    await expectNoAxeViolations(fixture);
  });
});
