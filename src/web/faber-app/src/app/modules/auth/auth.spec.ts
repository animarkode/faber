import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Auth } from './auth';
import { expectNoAxeViolations } from '../../shared/testing/axe';

describe('Auth', () => {
  let component: Auth;
  let fixture: ComponentFixture<Auth>;

  const root = () => fixture.nativeElement as HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Auth],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(Auth);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should link the brand to the home page', () => {
    const brand = root().querySelector<HTMLAnchorElement>('header a');

    expect(brand?.textContent?.trim()).toBe('microcivi.com');
    expect(brand?.getAttribute('href')).toBe('/');
  });

  it('should keep a single theme toggle, in the header', () => {
    expect(root().querySelectorAll('fb-theme-toggle').length).toBe(1);
    expect(root().querySelector('header fb-theme-toggle')).not.toBeNull();
  });

  it('should float the header so the content centres on the full height', () => {
    const header = root().querySelector('header');
    const main = root().querySelector('main');

    expect(header?.className).toContain('absolute');
    expect(main?.className).toContain('min-h-full');
    expect(main?.className).toContain('items-center');
  });

  it('should have no AXE violations', { timeout: 20000 }, async () => {
    await expectNoAxeViolations(fixture);
  });
});
