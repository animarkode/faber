import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { LinkedAccounts } from './linked-accounts';
import { AccountClient } from '../account-client';
import { AccountConfirm } from '../account-confirm';
import { confirmAnswering } from '../account-testing';
import { LinkedAccount } from '../account-contracts';
import { API_ROUTES } from '../../../../core/routes/api-routes';
import { expectNoAxeViolations } from '../../../../shared/testing/axe';

const accounts: LinkedAccount[] = [
  { providerAlias: 'google', displayName: 'Google', connected: true, linkedUsername: 'ada@gmail.com', linkAction: 'link-google' },
  { providerAlias: 'github', displayName: 'GitHub', connected: false, linkedUsername: null, linkAction: 'link-github' },
];

describe('LinkedAccounts', () => {
  let fixture: ComponentFixture<LinkedAccounts>;
  let http: HttpTestingController;

  const el = () => fixture.nativeElement as HTMLElement;

  async function setup(answer = true): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [LinkedAccounts],
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AccountConfirm, useValue: confirmAnswering(answer) }],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(LinkedAccounts);
    fixture.detectChanges();
    http.expectOne(API_ROUTES.auth.account.linkedAccounts).flush(accounts);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('should show Unlink for linked and Link for unlinked providers', async () => {
    await setup();

    const rows = el().querySelectorAll('[data-test="account"]');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Linked as ada@gmail.com');
    expect(rows[0].querySelector('[data-test="unlink"]')!.getAttribute('aria-label')).toBe('Unlink Google');
    expect(rows[1].querySelector('[data-test="link"]')!.getAttribute('aria-label')).toBe('Link GitHub');
  });

  it('should navigate to the link action', async () => {
    await setup();
    const start = vi.spyOn(TestBed.inject(AccountClient), 'startAction').mockImplementation(() => undefined);

    el().querySelector<HTMLElement>('[data-test="link"]')!.click();

    expect(start).toHaveBeenCalledWith('link-github');
  });

  it('should DELETE after confirmation', async () => {
    await setup();

    el().querySelector<HTMLElement>('[data-test="unlink"]')!.click();

    const req = http.expectOne(API_ROUTES.auth.account.linkedAccount('google'));
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    TestBed.tick();
    http.expectOne(API_ROUTES.auth.account.linkedAccounts).flush([]);
  });

  it('should not DELETE when declined', async () => {
    await setup(false);

    el().querySelector<HTMLElement>('[data-test="unlink"]')!.click();

    http.expectNone(API_ROUTES.auth.account.linkedAccount('google'));
  });

  it('should show the 409 detail', async () => {
    await setup();

    el().querySelector<HTMLElement>('[data-test="unlink"]')!.click();
    http.expectOne(API_ROUTES.auth.account.linkedAccount('google'))
      .flush({ detail: 'Set a password first.' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(el().querySelector('[data-test="error"]')!.textContent).toContain('Set a password first.');
  });

  it('should have no AXE violations', async () => {
    await setup();
    await expectNoAxeViolations(fixture);
  }, 20000);
});
