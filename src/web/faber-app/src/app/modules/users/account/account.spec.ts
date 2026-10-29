import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';

import { Account } from './account';
import { AuthStore } from '../../../core/auth/auth-store';
import { User } from '../../../core/auth/contracts/user';
import { expectNoAxeViolations } from '../../../shared/testing/axe';

describe('Account', () => {
  let fixture: ComponentFixture<Account>;

  const mockUser: User = {
    id: 'user-1',
    email: 'john@example.com',
    firstName: 'John',
    lastName: 'Doe',
    name: 'John Doe',
    roles: ['regular'],
    accountUrl: 'https://keycloak.example/realms/faber/account?referrer=faber-api',
  };

  const mockAuthStore = { user: signal<User | null>(mockUser) };

  beforeEach(async () => {
    mockAuthStore.user.set(mockUser);
    await TestBed.configureTestingModule({
      imports: [Account],
      providers: [{ provide: AuthStore, useValue: mockAuthStore }],
    }).compileComponents();

    fixture = TestBed.createComponent(Account);
    await fixture.whenStable();
  });

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('should show the name and e-mail Keycloak holds', () => {
    expect(text()).toContain('John Doe');
    expect(text()).toContain('john@example.com');
  });

  it('should offer no form: the profile is edited in Keycloak', () => {
    expect((fixture.nativeElement as HTMLElement).querySelector('form, input')).toBeNull();
  });

  it('should link to the Keycloak account console', () => {
    const link = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('a[data-test="manage-account"]');

    expect(link).not.toBeNull();
    expect(link!.getAttribute('href')).toBe(mockUser.accountUrl);
    expect(link!.textContent).toContain('Manage account');
  });

  it('should have no AXE violations (light)', async () => {
    await expectNoAxeViolations(fixture);
  }, 20000);

  it('should have no AXE violations (dark)', async () => {
    document.documentElement.classList.add('dark');
    try {
      await expectNoAxeViolations(fixture);
    } finally {
      document.documentElement.classList.remove('dark');
    }
  }, 20000);
});
