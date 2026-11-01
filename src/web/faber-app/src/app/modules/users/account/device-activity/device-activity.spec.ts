import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { DeviceActivity } from './device-activity';
import { AccountConfirm } from '../account-confirm';
import { confirmAnswering } from '../account-testing';
import { AccountDevice } from '../account-contracts';
import { API_ROUTES } from '../../../../core/routes/api-routes';
import { expectNoAxeViolations } from '../../../../shared/testing/axe';

function device(id: string, current: boolean): AccountDevice {
  return {
    id, device: 'Mac', os: 'macOS', osVersion: '15', browser: 'Firefox', ipAddress: '10.0.0.1',
    started: '2026-03-01T10:00:00Z', lastAccess: '2026-03-02T10:00:00Z', expires: null, current, mobile: false,
  };
}

describe('DeviceActivity', () => {
  let fixture: ComponentFixture<DeviceActivity>;
  let http: HttpTestingController;

  const el = () => fixture.nativeElement as HTMLElement;

  async function setup(devices: AccountDevice[], answer = true): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [DeviceActivity],
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AccountConfirm, useValue: confirmAnswering(answer) }],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DeviceActivity);
    fixture.detectChanges();
    http.expectOne(API_ROUTES.auth.account.devices).flush(devices);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('should list devices and badge the current one without a sign out button', async () => {
    await setup([device('a', true), device('b', false)]);

    const rows = el().querySelectorAll('[data-test="device"]');
    expect(rows.length).toBe(2);
    expect(rows[0].querySelector('[data-test="current"]')!.textContent).toContain('This device');
    expect(rows[0].querySelector('[data-test="sign-out"]')).toBeNull();
    expect(rows[1].querySelector('[data-test="sign-out"]')).not.toBeNull();
    expect(el().textContent).toContain('Mac · macOS 15 · Firefox');
    expect(el().textContent).toContain('10.0.0.1');
  });

  it('should show "No other devices" when only the current one exists', async () => {
    await setup([device('a', true)]);

    expect(el().querySelector('[data-test="empty"]')!.textContent).toContain('No other devices');
    expect(el().querySelector('[data-test="sign-out-others"]')).toBeNull();
  });

  it('should DELETE one session after confirmation', async () => {
    await setup([device('a', true), device('b', false)]);

    el().querySelector<HTMLElement>('[data-test="sign-out"]')!.click();

    const req = http.expectOne(API_ROUTES.auth.account.session('b'));
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    TestBed.tick();
    http.expectOne(API_ROUTES.auth.account.devices).flush([device('a', true)]);
  });

  it('should DELETE all other sessions after confirmation', async () => {
    await setup([device('a', true), device('b', false)]);

    el().querySelector<HTMLElement>('[data-test="sign-out-others"]')!.click();

    const req = http.expectOne(API_ROUTES.auth.account.otherSessions);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    TestBed.tick();
    http.expectOne(API_ROUTES.auth.account.devices).flush([device('a', true)]);
  });

  it('should not DELETE when declined', async () => {
    await setup([device('a', true), device('b', false)], false);

    el().querySelector<HTMLElement>('[data-test="sign-out"]')!.click();

    http.expectNone(API_ROUTES.auth.account.session('b'));
  });

  it('should show the problem detail when signing out fails', async () => {
    await setup([device('a', true), device('b', false)]);

    el().querySelector<HTMLElement>('[data-test="sign-out"]')!.click();
    http.expectOne(API_ROUTES.auth.account.session('b'))
      .flush({ detail: 'This is your current session.' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(el().querySelector('[data-test="error"]')!.textContent).toContain('current session');
  });

  it('should have no AXE violations', async () => {
    await setup([device('a', true), device('b', false)]);
    await expectNoAxeViolations(fixture);
  }, 20000);
});
