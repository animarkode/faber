import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { signal } from '@angular/core';
import { EMPTY, of } from 'rxjs';

import { AppShell } from './app-shell';
import { AuthStore } from '../../auth/auth-store';
import { AuthClient } from '../../auth/auth-client';

describe('AppShell', () => {
  let component: AppShell;
  let fixture: ComponentFixture<AppShell>;

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
    await TestBed.configureTestingModule({
      imports: [AppShell],
      providers: [
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute },
        {
          provide: AuthStore,
          useValue: {
            authenticated: signal(false),
            user: signal(null),
            loading: signal(false),
            error: signal(null),
            clearUser: vi.fn(),
            reload: vi.fn(),
          },
        },
        {
          provide: AuthClient,
          useValue: {
            signOut: vi.fn().mockReturnValue(of(null)),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppShell);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
