import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';

import { ForgotPassword } from './forgot-password';
import { AuthClient } from '../auth-client';
import { APP_ROUTES } from '../../../core/routes/app-routes';
import { expectNoAxeViolations } from '../../../shared/testing/axe';

describe('ForgotPassword', () => {
  let component: ForgotPassword;
  let fixture: ComponentFixture<ForgotPassword>;

  const mockAuthClient = {
    forgotPassword: vi.fn(),
  };

  beforeEach(async () => {
    vi.clearAllMocks();

    await TestBed.configureTestingModule({
      imports: [ForgotPassword],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: AuthClient, useValue: mockAuthClient },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ForgotPassword);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have loading false initially', () => {
    expect(component.loading()).toBe(false);
  });

  it('should have sent false initially', () => {
    expect(component.sent()).toBe(false);
  });

  it('should set sent on success', () => {
    mockAuthClient.forgotPassword.mockReturnValue(of(null));

    component.form.controls.email.setValue('john@example.com');
    component.onSubmit();

    expect(component.sent()).toBe(true);
    expect(component.error()).toBe(false);
  });

  it('should set error on failure', () => {
    mockAuthClient.forgotPassword.mockReturnValue(throwError(() => new Error('fail')));

    component.form.controls.email.setValue('john@example.com');
    component.onSubmit();

    expect(component.error()).toBe(true);
    expect(component.sent()).toBe(false);
  });

  it('should clear a previous error on resubmit', () => {
    component.error.set(true);
    mockAuthClient.forgotPassword.mockReturnValue(of(null));

    component.form.controls.email.setValue('john@example.com');
    component.onSubmit();

    expect(component.sent()).toBe(true);
    expect(component.error()).toBe(false);
  });

  describe('after the link is sent', () => {
    const root = () => fixture.nativeElement as HTMLElement;

    beforeEach(async () => {
      mockAuthClient.forgotPassword.mockReturnValue(of(null));
      component.form.controls.email.setValue('john@example.com');
      component.onSubmit();
      await fixture.whenStable();
    });

    it('should replace the form with a Check your email title and a Continue button only', () => {
      expect(root().querySelector('form')).toBeNull();
      expect(root().querySelector('h1')?.textContent?.trim()).toBe('Check your email');
      expect(root().querySelector('p')).toBeNull();

      const buttons = root().querySelectorAll('button');
      expect(buttons.length).toBe(1);
      expect(buttons[0].textContent?.trim()).toBe('Continue');
      expect(buttons[0].className).toContain('before:bg-primary');
    });

    it('should move focus to the new title', () => {
      expect(document.activeElement).toBe(root().querySelector('h1'));
    });

    it('should go to sign-in on Continue', () => {
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

      root().querySelector('button')!.click();

      expect(navigate).toHaveBeenCalledWith([APP_ROUTES.auth.signIn]);
    });

    it('should have no AXE violations', { timeout: 20000 }, async () => {
      await expectNoAxeViolations(fixture);
    });
  });
});
