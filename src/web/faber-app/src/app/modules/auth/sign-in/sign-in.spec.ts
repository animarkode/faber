import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';

import { SignIn } from './sign-in';
import { AuthClient } from '../auth-client';
import { APP_ROUTES } from '../../../core/routes/app-routes';
import { externalSignInErrorMessage } from '../external-sign-in/external-sign-in-errors';
import { expectNoAxeViolations } from '../../../shared/testing/axe';

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
        { provide: AuthClient, useValue: mockAuthClient },
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
  const CODES = [
    'provider_unavailable',
    'invalid_state',
    'provider_denied',
    'provider_error',
    'exchange_failed',
    'provider_email_unverified',
    'email_confirmation_required',
    'session_failed',
  ];

  async function create(params: Record<string, string>): Promise<{ fixture: ComponentFixture<SignIn>; router: Router }> {
    await TestBed.configureTestingModule({
      imports: [SignIn],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthClient, useValue: { signIn: vi.fn() } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(params) } } },
      ],
    }).compileComponents();

    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(SignIn);
    fixture.detectChanges();
    return { fixture, router };
  }

  it.each(CODES)('should show the message for %s', async code => {
    const { fixture } = await create({ error: code });

    expect(fixture.nativeElement.textContent).toContain(externalSignInErrorMessage(code));
  });

  it('should show the generic message for an unknown code', async () => {
    const { fixture } = await create({ error: 'something_new' });

    expect(fixture.nativeElement.textContent).toContain(externalSignInErrorMessage('something_new'));
  });

  it('should show no toast without an error param', async () => {
    const { fixture, router } = await create({});

    expect(fixture.componentInstance.toastMessage()).toBeNull();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('should remove the error param from the URL', async () => {
    const { router } = await create({ error: 'invalid_state' });

    expect(router.navigate).toHaveBeenCalledWith(
      [],
      expect.objectContaining({ queryParams: { error: null }, queryParamsHandling: 'merge', replaceUrl: true }),
    );
  });

  it('should have no AXE violations with an error shown', async () => {
    const { fixture } = await create({ error: 'provider_denied' });

    await expectNoAxeViolations(fixture);
  }, 20000);
});
