import { TestBed } from '@angular/core/testing';
import { THEME_COOKIE_DOMAIN, ThemeService } from './theme';

function mockMatchMedia(prefersDark: boolean): void {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: query === '(prefers-color-scheme: dark)' ? prefersDark : false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
}

function clearThemeCookie(): void {
  document.cookie = 'faber-theme=; Path=/; Max-Age=0';
}

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    clearThemeCookie();
    document.documentElement.classList.remove('dark');
    mockMatchMedia(false);
    TestBed.configureTestingModule({});
  });

  afterEach(() => {
    localStorage.clear();
    clearThemeCookie();
    vi.restoreAllMocks();
    document.documentElement.classList.remove('dark');
  });

  describe('initial state — no stored preference', () => {
    it('follows prefers-color-scheme (light)', () => {
      mockMatchMedia(false);
      expect(TestBed.inject(ThemeService).isDark()).toBe(false);
    });

    it('follows prefers-color-scheme (dark)', () => {
      mockMatchMedia(true);
      expect(TestBed.inject(ThemeService).isDark()).toBe(true);
    });

    it('applies .dark class when system prefers dark', () => {
      mockMatchMedia(true);
      TestBed.inject(ThemeService);
      TestBed.flushEffects();
      expect(document.documentElement.classList.contains('dark')).toBe(true);
    });

    it('does not apply .dark class when system prefers light', () => {
      mockMatchMedia(false);
      TestBed.inject(ThemeService);
      TestBed.flushEffects();
      expect(document.documentElement.classList.contains('dark')).toBe(false);
    });
  });

  describe('initial state — stored preference', () => {
    it('reads dark from localStorage regardless of system preference', () => {
      localStorage.setItem('faber-theme', 'dark');
      mockMatchMedia(false);
      expect(TestBed.inject(ThemeService).isDark()).toBe(true);
    });

    it('reads light from localStorage regardless of system preference', () => {
      localStorage.setItem('faber-theme', 'light');
      mockMatchMedia(true);
      expect(TestBed.inject(ThemeService).isDark()).toBe(false);
    });

    it('reads dark from the cookie regardless of system preference', () => {
      document.cookie = 'faber-theme=dark; Path=/';
      mockMatchMedia(false);
      expect(TestBed.inject(ThemeService).isDark()).toBe(true);
    });

    it('prefers the cookie over localStorage', () => {
      document.cookie = 'faber-theme=light; Path=/';
      localStorage.setItem('faber-theme', 'dark');
      expect(TestBed.inject(ThemeService).isDark()).toBe(false);
    });

    it('ignores an invalid cookie value and falls back to localStorage', () => {
      document.cookie = 'faber-theme=purple; Path=/';
      localStorage.setItem('faber-theme', 'dark');
      expect(TestBed.inject(ThemeService).isDark()).toBe(true);
    });
  });

  describe('cookie writing', () => {
    function spyOnCookieWrites(): string[] {
      const writes: string[] = [];
      vi.spyOn(document, 'cookie', 'set').mockImplementation((value: string) => {
        writes.push(value);
      });
      return writes;
    }

    it('writes the cookie on toggle without a domain', () => {
      const writes = spyOnCookieWrites();
      TestBed.inject(ThemeService).toggle();
      expect(writes).toEqual(['faber-theme=dark; Path=/; SameSite=Lax; Max-Age=31536000']);
    });

    it('persists the cookie so a new service reads it', () => {
      TestBed.inject(ThemeService).toggle();
      expect(document.cookie).toContain('faber-theme=dark');
    });

    it('adds Domain when a cookie domain is configured', () => {
      TestBed.overrideProvider(THEME_COOKIE_DOMAIN, { useValue: 'example.com' });
      const writes = spyOnCookieWrites();
      TestBed.inject(ThemeService).toggle();
      expect(writes).toEqual(['faber-theme=dark; Path=/; SameSite=Lax; Max-Age=31536000; Domain=example.com']);
    });
  });

  describe('toggle()', () => {
    let service: ThemeService;

    beforeEach(() => {
      service = TestBed.inject(ThemeService);
    });

    it('flips isDark from false to true', () => {
      service.toggle();
      expect(service.isDark()).toBe(true);
    });

    it('flips isDark from true to false on second call', () => {
      service.toggle();
      service.toggle();
      expect(service.isDark()).toBe(false);
    });

    it('adds .dark class when toggling to dark', () => {
      service.toggle();
      TestBed.flushEffects();
      expect(document.documentElement.classList.contains('dark')).toBe(true);
    });

    it('removes .dark class when toggling back to light', () => {
      service.toggle();
      service.toggle();
      TestBed.flushEffects();
      expect(document.documentElement.classList.contains('dark')).toBe(false);
    });

    it('persists "dark" to localStorage', () => {
      service.toggle();
      expect(localStorage.getItem('faber-theme')).toBe('dark');
    });

    it('persists "light" to localStorage on second toggle', () => {
      service.toggle();
      service.toggle();
      expect(localStorage.getItem('faber-theme')).toBe('light');
    });
  });

  it('exposes isDark as a readonly signal (no set method)', () => {
    const service = TestBed.inject(ThemeService);
    expect(typeof (service.isDark as unknown as Record<string, unknown>)['set']).toBe('undefined');
  });
});
