import { DOCUMENT } from '@angular/common';
import { effect, inject, Injectable, InjectionToken, signal } from '@angular/core';
import { environment } from '../../../environments/environment';

/** Parent domain for the shared theme cookie; null/empty means a host-only cookie (dev). */
export const THEME_COOKIE_DOMAIN = new InjectionToken<string | null>('THEME_COOKIE_DOMAIN', {
  factory: () => environment.cookieDomain || null,
});

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly cookieDomain = inject(THEME_COOKIE_DOMAIN);
  private readonly storageKey = 'faber-theme';
  private readonly oneYearInSeconds = 31536000;
  private readonly _isDark = signal(this.getInitial());
  readonly isDark = this._isDark.asReadonly();

  constructor() {
    effect(() => {
      this.document.documentElement.classList.toggle('dark', this._isDark());
    });
  }

  toggle(): void {
    const next = !this._isDark();
    this._isDark.set(next);
    const value = next ? 'dark' : 'light';
    try {
      localStorage.setItem(this.storageKey, value);
    } catch {
      // storage unavailable; the cookie still carries the choice
    }
    this.writeCookie(value);
  }

  private getInitial(): boolean {
    const stored = this.readCookie() ?? this.readStorage();
    if (stored) return stored === 'dark';
    try {
      return this.document.defaultView?.matchMedia('(prefers-color-scheme: dark)').matches ?? false;
    } catch {
      return false;
    }
  }

  private readCookie(): 'dark' | 'light' | null {
    try {
      const match = this.document.cookie
        .split(';')
        .map((part) => part.trim())
        .find((part) => part.startsWith(`${this.storageKey}=`));
      const value = match?.slice(this.storageKey.length + 1);
      return value === 'dark' || value === 'light' ? value : null;
    } catch {
      return null;
    }
  }

  private readStorage(): 'dark' | 'light' | null {
    try {
      const value = localStorage.getItem(this.storageKey);
      return value === 'dark' || value === 'light' ? value : null;
    } catch {
      return null;
    }
  }

  private writeCookie(value: 'dark' | 'light'): void {
    try {
      let cookie = `${this.storageKey}=${value}; Path=/; SameSite=Lax; Max-Age=${this.oneYearInSeconds}`;
      if (this.document.location.protocol === 'https:') cookie += '; Secure';
      if (this.cookieDomain) cookie += `; Domain=${this.cookieDomain}`;
      this.document.cookie = cookie;
    } catch {
      // cookies unavailable
    }
  }
}
