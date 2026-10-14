import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { SocialSignIn } from './social-sign-in';
import { API_ROUTES } from '../../../../core/routes/api-routes';
import { expectNoAxeViolations } from '../../../../shared/testing/axe';

describe('SocialSignIn', () => {
  let fixture: ComponentFixture<SocialSignIn>;
  let http: HttpTestingController;

  const root = () => fixture.nativeElement as HTMLElement;
  const links = () => Array.from(root().querySelectorAll('a'));

  async function render(providers: string[] | 'error'): Promise<void> {
    fixture = TestBed.createComponent(SocialSignIn);
    fixture.detectChanges();
    const req = http.expectOne(API_ROUTES.auth.external.providers);
    if (providers === 'error') {
      req.flush('boom', { status: 500, statusText: 'Server Error' });
    } else {
      req.flush({ providers });
    }
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SocialSignIn],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  it('should render nothing while loading', () => {
    fixture = TestBed.createComponent(SocialSignIn);
    fixture.detectChanges();

    expect(root().innerHTML.replace(/<!--.*?-->/g, '').trim()).toBe('');
  });

  it('should render nothing when no providers are available', async () => {
    await render([]);

    expect(links()).toHaveLength(0);
    expect(root().textContent).not.toContain('or');
    expect(root().querySelector('div')).toBeNull();
  });

  it('should render only github when only github is available', async () => {
    await render(['github']);

    expect(links()).toHaveLength(1);
    expect(links()[0].getAttribute('href')).toBe(API_ROUTES.auth.external.start('github'));
    expect(links()[0].textContent?.trim()).toBe('Continue with GitHub');
  });

  it('should render both providers in catalog order', async () => {
    await render(['github', 'google']);

    expect(links().map((a) => a.textContent?.trim())).toEqual([
      'Continue with Google',
      'Continue with GitHub',
    ]);
    expect(links().map((a) => a.getAttribute('href'))).toEqual([
      API_ROUTES.auth.external.start('google'),
      API_ROUTES.auth.external.start('github'),
    ]);
  });

  it('should ignore unknown provider aliases', async () => {
    await render(['gitlab', 'google']);

    expect(links()).toHaveLength(1);
    expect(links()[0].textContent?.trim()).toBe('Continue with Google');
  });

  it('should render nothing when the request fails', async () => {
    await render('error');

    expect(links()).toHaveLength(0);
    expect(root().querySelector('div')).toBeNull();
  });

  it('should have no AXE violations with both providers', { timeout: 20000 }, async () => {
    await render(['google', 'github']);

    await expectNoAxeViolations(fixture);
  });
});
