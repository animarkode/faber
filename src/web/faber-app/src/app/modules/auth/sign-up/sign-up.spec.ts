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
        { provide: ExternalProvidersStore, useValue: { providers: signal<string[]>([]) } },
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
