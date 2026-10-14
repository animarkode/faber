import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { NEVER, of, throwError } from 'rxjs';

import { ExternalCallback } from './external-callback';
import { AuthClient } from '../../auth-client';
import { AuthStore } from '../../../../core/auth/auth-store';
import { APP_ROUTES } from '../../../../core/routes/app-routes';
import { expectNoAxeViolations } from '../../../../shared/testing/axe';

describe('ExternalCallback', () => {
  let fixture: ComponentFixture<ExternalCallback>;
  let router: Router;

  const mockAuthClient = { refresh: vi.fn() };
  const mockAuthStore = { reload: vi.fn() };

  beforeEach(async () => {
    vi.clearAllMocks();

    await TestBed.configureTestingModule({
      imports: [ExternalCallback],
      providers: [
        provideRouter([]),
        { provide: AuthClient, useValue: mockAuthClient },
        { provide: AuthStore, useValue: mockAuthStore },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture = TestBed.createComponent(ExternalCallback);
  });

  it('should reload the session and go home when the refresh succeeds', () => {
    mockAuthClient.refresh.mockReturnValue(of({ accessToken: 'token' }));

    fixture.detectChanges();

    expect(mockAuthStore.reload).toHaveBeenCalledTimes(1);
    expect(router.navigate).toHaveBeenCalledWith([APP_ROUTES.home], { replaceUrl: true });
  });

  it('should return to sign-in with session_failed when the refresh fails', () => {
    mockAuthClient.refresh.mockReturnValue(throwError(() => new Error('401')));

    fixture.detectChanges();

    expect(mockAuthStore.reload).not.toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith([APP_ROUTES.auth.signIn], {
      queryParams: { error: 'session_failed' },
      replaceUrl: true,
    });
  });

  it('should show a status message while signing in', () => {
    mockAuthClient.refresh.mockReturnValue(NEVER);

    fixture.detectChanges();

    const status = fixture.nativeElement.querySelector('[role="status"]');
    expect(status.textContent).toContain('Signing you in…');
  });

  it('should have no AXE violations', async () => {
    mockAuthClient.refresh.mockReturnValue(NEVER);
    fixture.detectChanges();
    await expectNoAxeViolations(fixture);
  }, 20000);
});
