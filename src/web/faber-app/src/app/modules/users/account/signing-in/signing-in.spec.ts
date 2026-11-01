import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { SigningIn } from './signing-in';
import { AccountClient } from '../account-client';
import { AccountConfirm } from '../account-confirm';
import { confirmAnswering } from '../account-testing';
import { CredentialGroup } from '../account-contracts';
import { API_ROUTES } from '../../../../core/routes/api-routes';
import { expectNoAxeViolations } from '../../../../shared/testing/axe';

const groups: CredentialGroup[] = [
  {
    type: 'password', category: 'basic-authentication', displayName: '${password}', helpText: null,
    createAction: null, updateAction: 'password', removeable: false,
    credentials: [{ id: 'p1', userLabel: null, createdDate: '2026-01-02T10:00:00Z' }],
  },
  {
    type: 'webauthn-passwordless', category: 'passwordless', displayName: 'webauthn-passwordless', helpText: 'Sign in with a passkey.',
    createAction: 'passkey', updateAction: null, removeable: true,
    credentials: [{ id: 'k1', userLabel: 'MacBook', createdDate: '2026-02-03T10:00:00Z' }],
  },
  {
    type: 'otp', category: 'two-factor', displayName: 'otp', helpText: null,
    createAction: 'totp', updateAction: null, removeable: true, credentials: [],
  },
];

describe('SigningIn', () => {
  let fixture: ComponentFixture<SigningIn>;
  let http: HttpTestingController;
  let confirm: ReturnType<typeof confirmAnswering>;

  const el = () => fixture.nativeElement as HTMLElement;

  async function setup(answer = true): Promise<void> {
    confirm = confirmAnswering(answer);
    await TestBed.configureTestingModule({
      imports: [SigningIn],
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AccountConfirm, useValue: confirm }],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SigningIn);
    fixture.detectChanges();
    http.expectOne(API_ROUTES.auth.account.credentials).flush(groups);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('should render a group per credential type with its items', async () => {
    await setup();

    expect(el().querySelectorAll('[data-test="group"]').length).toBe(3);
    expect(el().textContent).toContain('MacBook');
    expect(el().textContent).toContain('Passkey');
  });

  it('should show an empty state for a type with nothing stored', async () => {
    await setup();

    const groupsEls = el().querySelectorAll('[data-test="group"]');
    expect(groupsEls[2].querySelector('[data-test="empty"]')).not.toBeNull();
    expect(groupsEls[2].querySelector('[data-test="create"]')!.textContent).toContain('Set up');
    expect(groupsEls[1].querySelector('[data-test="create"]')!.textContent).toContain('Add');
  });

  it('should only offer Remove where removeable, with a labelled button', async () => {
    await setup();

    const removes = el().querySelectorAll('[data-test="remove"]');
    expect(removes.length).toBe(1);
    expect(removes[0].getAttribute('aria-label')).toBe("Remove passkey 'MacBook'");
  });

  it('should navigate to the create and update actions', async () => {
    await setup();
    const start = vi.spyOn(TestBed.inject(AccountClient), 'startAction').mockImplementation(() => undefined);

    el().querySelector<HTMLElement>('[data-test="update"]')!.click();
    el().querySelectorAll<HTMLElement>('[data-test="create"]')[0].click();

    expect(start).toHaveBeenNthCalledWith(1, 'password');
    expect(start).toHaveBeenNthCalledWith(2, 'passkey');
  });

  it('should DELETE after confirmation and reload', async () => {
    await setup();

    el().querySelector<HTMLElement>('[data-test="remove"]')!.click();

    const req = http.expectOne(API_ROUTES.auth.account.credential('k1'));
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    TestBed.tick();
    http.expectOne(API_ROUTES.auth.account.credentials).flush([]);
  });

  it('should not DELETE when the confirmation is declined', async () => {
    await setup(false);

    el().querySelector<HTMLElement>('[data-test="remove"]')!.click();

    http.expectNone(API_ROUTES.auth.account.credential('k1'));
  });

  it('should show the 409 detail', async () => {
    await setup();

    el().querySelector<HTMLElement>('[data-test="remove"]')!.click();
    http.expectOne(API_ROUTES.auth.account.credential('k1'))
      .flush({ detail: 'You cannot remove your last sign-in method.' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(el().querySelector('[data-test="error"]')!.textContent).toContain('last sign-in method');
  });

  it('should have no AXE violations', async () => {
    await setup();
    await expectNoAxeViolations(fixture);
  }, 20000);
});
