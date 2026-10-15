import { describe, expect, it } from 'vitest';
import {
  EXTERNAL_SIGN_IN_SESSION_FAILED,
  externalSignInErrorMessage,
} from './external-sign-in-errors';

const CODES = [
  'provider_unavailable',
  'invalid_state',
  'provider_denied',
  'provider_error',
  'exchange_failed',
  'provider_email_unverified',
  'email_confirmation_required',
  EXTERNAL_SIGN_IN_SESSION_FAILED,
];

describe('externalSignInErrorMessage', () => {
  it('returns a non-empty message for every known code', () => {
    for (const code of CODES) {
      expect(externalSignInErrorMessage(code)?.length).toBeGreaterThan(0);
    }
  });

  it('returns a distinct message per code', () => {
    const messages = new Set(CODES.map((code) => externalSignInErrorMessage(code)));
    expect(messages.size).toBe(CODES.length);
  });

  it('returns a generic message for unknown codes', () => {
    const generic = externalSignInErrorMessage('something_else');
    expect(generic).toBeTruthy();
    expect(CODES.map((c) => externalSignInErrorMessage(c))).not.toContain(generic);
    expect(externalSignInErrorMessage('toString')).toBe(generic);
  });

  it('returns null for null, undefined and empty codes', () => {
    expect(externalSignInErrorMessage(null)).toBeNull();
    expect(externalSignInErrorMessage(undefined)).toBeNull();
    expect(externalSignInErrorMessage('')).toBeNull();
  });
});
