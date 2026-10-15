import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { ExternalCallback } from './external-callback';
import { AuthStore } from '../../../../core/auth/auth-store';
import { APP_ROUTES } from '../../../../core/routes/app-routes';
import { expectNoAxeViolations } from '../../../../shared/testing/axe';

describe('ExternalCallback', () => {
  let fixture: ComponentFixture<ExternalCallback>;
  let router: Router;

  const loading = signal(true);
  const authenticated = signal(false);
  const mockAuthStore = { loading, authenticated, reload: vi.fn() };

  /** Lets the store's `/auth/me` (and the interceptor's refresh behind it) settle. */
  async function settle(signedIn: boolean): Promise<void> {
    authenticated.set(signedIn);
    loading.set(false);
    await fixture.whenStable();
  }

  beforeEach(async () => {
    vi.clearAllMocks();
    loading.set(true);
    authenticated.set(false);

    await TestBed.configureTestingModule({
      imports: [ExternalCallback],
      providers: [provideRouter([]), { provide: AuthStore, useValue: mockAuthStore }],
    }).compileComponents();

    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture = TestBed.createComponent(ExternalCallback);
  });

  it('should wait for the in-flight session check instead of refreshing again', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    expect(mockAuthStore.reload).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('should go home once the session resolves to a user', async () => {
    fixture.detectChanges();
    await settle(true);

    expect(router.navigate).toHaveBeenCalledWith([APP_ROUTES.home], { replaceUrl: true });
  });

  it('should return to sign-in with session_failed when no session can be restored', async () => {
    fixture.detectChanges();
    await settle(false);

    expect(router.navigate).toHaveBeenCalledWith([APP_ROUTES.auth.signIn], {
      queryParams: { error: 'session_failed' },
      replaceUrl: true,
    });
  });

  it('should re-check the session when the store already settled without a user', async () => {
    loading.set(false);

    fixture.detectChanges();

    expect(mockAuthStore.reload).toHaveBeenCalledTimes(1);
  });

  it('should fill the status region only after it is rendered', async () => {
    const status = (): string =>
      fixture.nativeElement.querySelector('[role="status"]').textContent.trim();

    fixture.detectChanges();
    expect(status()).toBe('');

    await new Promise((resolve) => setTimeout(resolve, 200));
    fixture.detectChanges();
    expect(status()).toBe('Signing you in…');
  });

  it('should have no AXE violations', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    await expectNoAxeViolations(fixture);
  }, 20000);
});
