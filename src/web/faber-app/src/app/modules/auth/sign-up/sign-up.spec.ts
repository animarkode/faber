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
    hasKnownProviders: signal(providers.length > 0),
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

  it('should stay on the page after registering, until Continue', () => {
    mockAuthClient.signUp.mockReturnValue(of({}));

    component.form.setValue(validForm);
    component.onSubmit();

    expect(component.registered()).toBe(true);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  describe('after registering', () => {
    const root = () => fixture.nativeElement as HTMLElement;

    beforeEach(async () => {
      mockAuthClient.signUp.mockReturnValue(of({}));
      component.form.setValue(validForm);
      component.onSubmit();
      await fixture.whenStable();
    });

    it('should replace the form with a Check your email title and a Continue button only', () => {
      expect(root().querySelector('form')).toBeNull();
      expect(root().querySelector('h1')?.textContent?.trim()).toBe('Check your email');
      expect(root().querySelector('p')).toBeNull();
      expect(root().querySelectorAll('a').length).toBe(0);

      const buttons = root().querySelectorAll('button');
      expect(buttons.length).toBe(1);
      expect(buttons[0].textContent?.trim()).toBe('Continue');
      expect(buttons[0].className).toContain('before:bg-primary');
    });

    it('should move focus to the new title', () => {
      expect(document.activeElement).toBe(root().querySelector('h1'));
    });

    it('should go to sign-in on Continue', () => {
      root().querySelector('button')!.click();

      expect(router.navigate).toHaveBeenCalledWith([APP_ROUTES.auth.signIn]);
    });

    it('should have no AXE violations', { timeout: 20000 }, async () => {
      await expectNoAxeViolations(fixture);
    });
  });

  it('should set error messages on registration error', () => {
    // The shape FastEndpoints returns: messages grouped by field.
    const errorResponse = new HttpErrorResponse({
      error: {
        statusCode: 400,
        message: 'One or more errors occurred!',
        errors: { username: ['Username already exists'], email: ['Email already exists'] },
      },
      status: 400,
    });
    mockAuthClient.signUp.mockReturnValue(throwError(() => errorResponse));

    component.form.setValue(validForm);
    component.onSubmit();

    expect(component.invalidRegister()).toBe(true);
    expect(component.errors()).toEqual(['Username already exists', 'Email already exists']);
    expect(component.errorMessage()).toBe('Username already exists, Email already exists');
  });

  it('should fall back to a generic message when the error carries no field messages', () => {
    mockAuthClient.signUp.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 429 })));

    component.form.setValue(validForm);
    component.onSubmit();

    expect(component.errors()).toEqual([]);
    expect(component.errorMessage()).toBe('Sign-up failed. Try again.');
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

describe('SignUp provider choice and email form', () => {
  let fixture: ComponentFixture<SignUp>;

  const root = () => fixture.nativeElement as HTMLElement;
  const form = () => root().querySelector('form');
  const providers = () => root().querySelector('app-social-sign-in');
  const button = (text: string) =>
    Array.from(root().querySelectorAll('button')).find((b) => b.textContent?.trim() === text);
  const accountLink = () =>
    Array.from(root().querySelectorAll('a')).find((a) => a.textContent?.trim() === 'Sign in');
  const accountQuestion = () =>
    Array.from(root().querySelectorAll('p')).find((p) => p.textContent?.trim() === "Already have an account?");

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

  async function chooseEmail(): Promise<void> {
    button('Continue with email')!.click();
    await fixture.whenStable();
  }

  it('should show nothing below the heading while providers load', async () => {
    await render(providersStore([], false));

    expect(form()).toBeNull();
    expect(providers()).toBeNull();
    expect(accountLink()).toBeUndefined();
  });

  it('should show the form straight away when no provider is available', async () => {
    await render(providersStore([]));

    expect(form()).not.toBeNull();
    expect(button('Continue with email')).toBeUndefined();
    expect(accountLink()).toBeDefined();
  });

  it('should offer providers and a primary e-mail button instead of the form', async () => {
    await render(providersStore(['google']));

    expect(form()).toBeNull();
    expect(providers()).not.toBeNull();
    expect(button('Continue with email')!.className).toContain('before:bg-primary');
    expect(button('Continue with email')!.querySelector('lucide-icon[name="at-sign"]')).not.toBeNull();
  });

  it('should show the account question with a menu-style button on the choice', async () => {
    await render(providersStore(['google']));

    expect(accountQuestion()).toBeDefined();
    expect(accountLink()!.className).toContain('before:bg-surface');
    expect(accountLink()!.className).toContain('h-12');
  });

  it('should replace the providers with the form and focus its first field', async () => {
    await render(providersStore(['google']));

    await chooseEmail();

    expect(form()).not.toBeNull();
    expect(providers()).toBeNull();
    expect(document.activeElement).toBe(form()!.querySelector('input'));
  });

  it('should drop the account question once the form replaces the choice', async () => {
    await render(providersStore(['google']));

    await chooseEmail();

    expect(accountLink()).toBeUndefined();
    expect(accountQuestion()).toBeUndefined();
  });

  it('should label the submit button Continue', async () => {
    await render(providersStore([]));

    expect(form()!.querySelector('button[type="submit"]')!.textContent?.trim()).toBe('Continue');
  });

  it('should have no AXE violations on the provider choice', { timeout: 20000 }, async () => {
    await render(providersStore(['google', 'github']));

    await expectNoAxeViolations(fixture);
  });

  it('should have no AXE violations on the email form', { timeout: 20000 }, async () => {
    await render(providersStore(['google', 'github']));
    await chooseEmail();

    await expectNoAxeViolations(fixture);
  });
});
