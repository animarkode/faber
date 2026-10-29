import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AppUserMenu } from './app-user-menu';
import { AuthClient } from '../../../auth/auth-client';
import { User } from '../../../auth/contracts/user';
import { APP_ROUTES } from '../../../routes/app-routes';
import { expectNoAxeViolations } from '../../../../shared/testing/axe';

describe('AppUserMenu', () => {
  let component: AppUserMenu;
  let fixture: ComponentFixture<AppUserMenu>;

  const mockUser: User = {
    id: '1',
    email: 'john@example.com',
    firstName: 'John',
    lastName: 'Doe',
    name: 'John Doe',
    roles: ['regular'],
    accountUrl: 'https://keycloak.example/realms/faber/account',
  };

  const mockAuthClient = {
    signOut: vi.fn().mockReturnValue(of(null)),
  };

  beforeEach(async () => {
    vi.clearAllMocks();

    await TestBed.configureTestingModule({
      imports: [AppUserMenu],
      providers: [
        provideRouter([{ path: '**', redirectTo: '' }]),
        { provide: AuthClient, useValue: mockAuthClient },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppUserMenu);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('user', mockUser);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display the provided user', () => {
    expect(component.user()).toEqual(mockUser);
  });

  it('should call signOut when logout is invoked', () => {
    component.logout();
    expect(mockAuthClient.signOut).toHaveBeenCalledOnce();
  });

  it('should render an Account link', () => {
    const el = fixture.nativeElement as HTMLElement;
    const links = Array.from(el.querySelectorAll('a')) as HTMLAnchorElement[];
    const accountLink = links.find(a => a.textContent?.trim() === 'Account');
    expect(accountLink).toBeTruthy();
  });

  it('should link Account to APP_ROUTES.account', () => {
    const el = fixture.nativeElement as HTMLElement;
    const links = Array.from(el.querySelectorAll('a')) as HTMLAnchorElement[];
    const accountLink = links.find(a => a.textContent?.trim() === 'Account');
    expect(accountLink?.getAttribute('href')).toBe(APP_ROUTES.account);
  });

  it('should not render a Settings link', () => {
    const el = fixture.nativeElement as HTMLElement;
    const links = Array.from(el.querySelectorAll('a')) as HTMLAnchorElement[];
    const settingsLink = links.find(a => a.textContent?.trim() === 'Settings');
    expect(settingsLink).toBeUndefined();
  });

  it('should render on the shared glass surface', () => {
    expect((fixture.nativeElement as HTMLElement).classList.contains('glass-surface')).toBe(true);
  });

  it('should have no AXE violations', async () => {
    await expectNoAxeViolations(fixture);
  }, 20_000);
});
