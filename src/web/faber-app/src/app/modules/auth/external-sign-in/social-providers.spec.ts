import { describe, expect, it } from 'vitest';
import { SOCIAL_PROVIDERS, isKnownProvider } from './social-providers';

describe('social providers', () => {
  it('lists google then github', () => {
    expect(SOCIAL_PROVIDERS.map((p) => p.id)).toEqual(['google', 'github']);
  });

  it('gives github a dark-theme asset and google none', () => {
    expect(SOCIAL_PROVIDERS.find((p) => p.id === 'github')?.iconDark).toBe(
      'brands/github-mark-white.svg',
    );
    expect(SOCIAL_PROVIDERS.find((p) => p.id === 'google')?.iconDark).toBeUndefined();
  });

  it('recognises only known aliases', () => {
    expect(isKnownProvider('google')).toBe(true);
    expect(isKnownProvider('github')).toBe(true);
    expect(isKnownProvider('facebook')).toBe(false);
    expect(isKnownProvider('')).toBe(false);
  });
});
