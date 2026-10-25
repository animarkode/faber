import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';

import { SignUp } from './sign-up';
import { AuthClient } from '../auth-client';
import { ExternalProvidersStore } from '../external-sign-in/external-providers-store';
import { APP_ROUTES } from '../../../core/routes/app-routes';
import { expectNoAxeViolations } from '../../../shared/testing/axe';

/** A settled providers store reporting the given aliases. */
function providersStore(providers: string[], settled = true) {
  return {
    providers: signal(providers),
    settled: signal(settled),
  };
}

describe('SignUp', () => {
  let component: SignUp;
  let fixture: ComponentFixture<SignUp>;
  let router: Router;

  const mockAuthClient = {
    signUp: vi.fn(),
  };

  const validForm = {
    userName: 'testuser',
    firstName: 'John',
    lastName: 'Doe',
    email: 'john@example.com',
    password: 'password123',
    confirmPassword: 'password123',
  };

  beforeEach(async () => {
    vi.clearAllMocks();

    await TestBed.configureTestingModule({
      imports: [SignUp],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthClient, useValue: mockAuthClient },
        { provide: ExternalProvidersStore, useValue: providersStore([]) },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SignUp);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should not call signUp when form is invalid', () => {
    component.onSubmit();
    expect(mockAuthClient.signUp).not.toHaveBeenCalled();
  });

  it('should navigate to sign-in on successful registration', () => {
    mockAuthClient.signUp.mockReturnValue(of({}));

    component.form.setValue(validForm);
    component.onSubmit();

    expect(router.navigate).toHaveBeenCalledWith([APP_ROUTES.auth.signIn]);
  });

  it('should set error messages on registration error', () => {
    const errorResponse = new HttpErrorResponse({
      error: { errors: [{ errorMessage: 'Username taken' }, { errorMessage: 'Email exists' }] },
      status: 400,
    });
    mockAuthClient.signUp.mockReturnValue(throwError(() => errorResponse));

    component.form.setValue(validForm);
    component.onSubmit();

    expect(component.invalidRegister()).toBe(true);
    expect(component.errors()).toEqual(['Username taken', 'Email exists']);
  });

  it('should detect password mismatch on confirmPassword control', () => {
    component.form.setValue({ ...validForm, confirmPassword: 'different123' });

    expect(component.form.errors).toBeNull();
    expect(component.form.controls.password.hasError('mismatch')).toBe(false);
    expect(component.form.controls.confirmPassword.hasError('mismatch')).toBe(true);
  });

  it('should mark all controls as touched when submitting an invalid form', () => {
    component.onSubmit();

    expect(component.form.controls.userName.touched).toBe(true);
    expect(component.form.controls.email.touched).toBe(true);
    expect(component.form.controls.password.touched).toBe(true);
    expect(component.form.controls.confirmPassword.touched).toBe(true);
  });
});

describe('SignUp providers and form', () => {
  let fixture: ComponentFixture<SignUp>;

  const root = () => fixture.nativeElement as HTMLElement;
  const form = () => root().querySelector('form');
  const providers = () => root().querySelector('app-social-sign-in');
  const signInLink = () =>
    Array.from(root().querySelectorAll('a')).find((a) => a.textContent?.trim() === 'Sign in');

  async function render(store: ReturnType<typeof providersStore>): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [SignUp],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthClient, useValue: { signUp: vi.fn() } },
        { provide: ExternalProvidersStore, useValue: store },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SignUp);
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

  it('should link back to sign-in', async () => {
    await render(providersStore(['google']));

    expect(signInLink()).toBeDefined();
  });

  it('should have no AXE violations with providers and form', { timeout: 20000 }, async () => {
    await render(providersStore(['google', 'github']));

    await expectNoAxeViolations(fixture);
  });
});
