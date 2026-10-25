import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { of, throwError, Subject } from 'rxjs';

import { VerifyEmail } from './verify-email';
import { AuthClient } from '../auth-client';
import { APP_ROUTES } from '../../../core/routes/app-routes';

describe('VerifyEmail', () => {
  const mockAuthClient = {
    verifyEmail: vi.fn(),
  };

  function setup(queryParams: Record<string, string> = {}) {
    vi.clearAllMocks();

    TestBed.configureTestingModule({
      imports: [VerifyEmail],
      providers: [
        provideRouter([]),
        { provide: AuthClient, useValue: mockAuthClient },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParams } } },
      ],
    });

    const fixture = TestBed.createComponent(VerifyEmail);
    const component = fixture.componentInstance;
    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.detectChanges();
    return { fixture, component, router };
  }

  it('should set error when no key is provided', () => {
    const { component } = setup();
    expect(component.error()).toBe('Email or token not provided.');
    expect(component.verifying()).toBe(false);
  });

  it('should set verified on successful verification', () => {
    mockAuthClient.verifyEmail.mockReturnValue(of(null));

    const { component } = setup({ key: 'valid-key' });

    expect(component.verified()).toBe(true);
    expect(component.verifying()).toBe(false);
    expect(component.error()).toBeNull();
  });

  it('should show user-friendly error on verification failure', () => {
    mockAuthClient.verifyEmail.mockReturnValue(throwError(() => new Error('server error')));

    const { component } = setup({ key: 'expired-key' });

    expect(component.error()).toBe('Verification failed. The link may have expired.');
    expect(component.verifying()).toBe(false);
  });

  it('should navigate to sign-in using APP_ROUTES', () => {
    mockAuthClient.verifyEmail.mockReturnValue(of(null));

    const { component, router } = setup({ key: 'valid-key' });
    component.navigateToSignIn();

    expect(router.navigate).toHaveBeenCalledWith([APP_ROUTES.auth.signIn]);
  });

  it('should show an opaque verified pill and a Continue button to sign-in, with no extra text', () => {
    mockAuthClient.verifyEmail.mockReturnValue(of(null));

    const { fixture, router } = setup({ key: 'valid-key' });
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    const pill = root.querySelector('span.text-success-text')!;
    expect(pill.className).toContain('bg-success-soft');
    expect(pill.className).not.toMatch(/bg-success\/\d/);
    expect(root.querySelector('p')).toBeNull();

    const button = root.querySelector<HTMLButtonElement>('button')!;
    expect(button.textContent?.trim()).toBe('Continue');
    button.click();
    expect(router.navigate).toHaveBeenCalledWith([APP_ROUTES.auth.signIn]);
  });

  it('should keep the Verifying pill text AA-safe on the dark card fill', () => {
    mockAuthClient.verifyEmail.mockReturnValue(new Subject());

    const { fixture } = setup({ key: 'valid-key' });

    const pill = fixture.nativeElement.querySelector('span.bg-card');
    expect(pill).toBeTruthy();
    expect(pill.className).toContain('dark:text-foreground');
  });
});
