import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';

import { Account } from './account';
import { AccountClient } from './account-client';
import { AccountConfirm } from './account-confirm';
import { confirmAnswering } from './account-testing';

describe('Account', () => {
  let fixture: ComponentFixture<Account>;

  const el = () => fixture.nativeElement as HTMLElement;
  const status = () => el().querySelector('[data-test="status"]')!;

  async function setup(query: Record<string, string>, answer = true): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [Account],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { queryParamMap: of(convertToParamMap(query)) } },
        { provide: AccountConfirm, useValue: confirmAnswering(answer) },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(Account);
    fixture.detectChanges();
  }

  it('should compose the settings sections under one h1', async () => {
    await setup({});

    expect(el().querySelectorAll('h1').length).toBe(1);
    for (const tag of ['app-personal-info', 'app-signing-in', 'app-device-activity', 'app-linked-accounts']) {
      expect(el().querySelector(tag)).not.toBeNull();
    }
    expect(el().querySelector('[data-test="manage-account"]')).toBeNull();
  });

  it('should announce nothing without a returned action', async () => {
    await setup({});

    expect(status().getAttribute('role')).toBe('status');
    expect(status().textContent?.trim()).toBe('');
  });

  it.each([
    [{ action: 'password', status: 'success' }, 'Password updated'],
    [{ action: 'passkey', status: 'cancelled' }, 'Cancelled'],
    [{ action: 'totp', status: 'error' }, 'Something went wrong'],
  ])('should show the outcome of %j', async (query, message) => {
    await setup(query);

    expect(status().textContent).toContain(message);
  });

  it('should navigate to the delete action only after confirmation', async () => {
    await setup({});
    const start = vi.spyOn(TestBed.inject(AccountClient), 'startAction').mockImplementation(() => undefined);

    el().querySelector<HTMLElement>('[data-test="delete-account"]')!.click();

    expect(start).toHaveBeenCalledWith('delete');
  });

  it('should stay put when deleting is declined', async () => {
    await setup({}, false);
    const start = vi.spyOn(TestBed.inject(AccountClient), 'startAction').mockImplementation(() => undefined);

    el().querySelector<HTMLElement>('[data-test="delete-account"]')!.click();

    expect(start).not.toHaveBeenCalled();
  });
});
