import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { signal } from '@angular/core';
import { EMPTY, of } from 'rxjs';

import { AppNavbar } from './app-navbar';
import { AuthStore } from '../../auth/auth-store';
import { AuthClient } from '../../auth/auth-client';
import { API_ROUTES } from '../../routes/api-routes';
import { ThemeService } from '../../services/theme';

describe('AppNavbar', () => {
  let component: AppNavbar;
  let fixture: ComponentFixture<AppNavbar>;

  const mockAuthStore = {
    authenticated: signal(false),
    user: signal(null),
    loading: signal(false),
    error: signal(null),
    clearUser: vi.fn(),
    reload: vi.fn(),
  };

  const mockAuthClient = {
    signOut: vi.fn().mockReturnValue(of(null)),
  };

  const mockThemeService = {
    isDark: signal(false),
    toggle: vi.fn(),
  };

  const mockRouter = {
    navigate: vi.fn().mockResolvedValue(true),
    navigateByUrl: vi.fn().mockResolvedValue(true),
    events: EMPTY,
    createUrlTree: vi.fn().mockReturnValue({}),
    serializeUrl: vi.fn().mockReturnValue(''),
  };

  const mockActivatedRoute = {
    snapshot: { params: {}, data: {}, url: [], queryParams: {} },
    params: EMPTY,
    queryParams: EMPTY,
    url: EMPTY,
    fragment: EMPTY,
    data: EMPTY,
    outlet: 'primary',
    component: null,
    routeConfig: null,
    root: null as unknown as ActivatedRoute,
    parent: null,
    firstChild: null,
    children: [],
    pathFromRoot: [],
    paramMap: { get: () => null, getAll: () => [], has: () => false, keys: [] },
    queryParamMap: { get: () => null, getAll: () => [], has: () => false, keys: [] },
  };

  beforeEach(async () => {
    vi.clearAllMocks();

    await TestBed.configureTestingModule({
      imports: [AppNavbar],
      providers: [
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute },
        { provide: AuthStore, useValue: mockAuthStore },
        { provide: AuthClient, useValue: mockAuthClient },
        { provide: ThemeService, useValue: mockThemeService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppNavbar);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have isUserMenuDropdownVisible false by default', () => {
    expect(component.isUserMenuDropdownVisible()).toBe(false);
  });

  it('should have isDrawerOpen false by default', () => {
    expect(component.isDrawerOpen()).toBe(false);
  });

  it('should toggle user menu dropdown visibility', () => {
    component.toggleUserMenuDropdown();
    expect(component.isUserMenuDropdownVisible()).toBe(true);

    component.toggleUserMenuDropdown();
    expect(component.isUserMenuDropdownVisible()).toBe(false);
  });

  it('should open drawer', () => {
    component.openDrawer();
    expect(component.isDrawerOpen()).toBe(true);
  });

  it('should close drawer', () => {
    component.openDrawer();
    component.closeDrawer();
    expect(component.isDrawerOpen()).toBe(false);
  });

  it('should toggle drawer open and closed', () => {
    component.toggleDrawer();
    expect(component.isDrawerOpen()).toBe(true);

    component.toggleDrawer();
    expect(component.isDrawerOpen()).toBe(false);
  });

  it('should send a signed-out visitor to Keycloak through the API for sign-in and sign-up', () => {
    const hrefs = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a'))
      .map((a) => a.getAttribute('href'));

    expect(hrefs).toContain(API_ROUTES.auth.login());
    expect(hrefs).toContain(API_ROUTES.auth.register);
  });

  it('should default title to "microcivi.com"', () => {
    expect(component.title()).toBe('microcivi.com');
  });

  it('should accept custom title input', () => {
    fixture.componentRef.setInput('title', 'My App');
    expect(component.title()).toBe('My App');
  });
});
