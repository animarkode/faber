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

  it('should keep the theme toggle in the header on wider screens only', () => {
    const wrapper = root().querySelector('header fb-theme-toggle')?.parentElement;

    expect(wrapper?.className).toContain('max-sm:hidden');
  });

  it('should move the theme toggle below the form on a phone', () => {
    const footer = root().querySelector('footer');

    expect(footer?.querySelector('fb-theme-toggle')).not.toBeNull();
    expect(footer?.className).toContain('sm:hidden');
  });

  it('should have no AXE violations', { timeout: 20000 }, async () => {
    await expectNoAxeViolations(fixture);
  });
});
