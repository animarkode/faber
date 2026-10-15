export const EXTERNAL_SIGN_IN_SESSION_FAILED = 'session_failed';

const GENERIC_MESSAGE = "We couldn't sign you in. Please try again.";

const MESSAGES: Readonly<Record<string, string>> = {
  provider_unavailable:
    'This sign-in option is not available right now. Try again later or use another way to sign in.',
  invalid_state: 'Your sign-in attempt expired or was interrupted. Please start again.',
  provider_denied: 'Sign-in was cancelled. You can try again whenever you are ready.',
  provider_error:
    'The provider reported a problem while signing you in. Please try again in a moment.',
  exchange_failed: "We couldn't complete the sign-in with the provider. Please try again.",
  provider_email_unverified:
    "The provider hasn't verified your e-mail address, so we can't sign you in with it. Verify it with the provider or use another way to sign in.",
  email_confirmation_required:
    'An account with this e-mail already exists but is not confirmed yet. Confirm your e-mail from the link we sent, then try again.',
  [EXTERNAL_SIGN_IN_SESSION_FAILED]:
    "We couldn't finish signing you in. Please try again.",
};

export function externalSignInErrorMessage(code: string | null | undefined): string | null {
  if (!code) {
    return null;
  }
  return Object.hasOwn(MESSAGES, code) ? MESSAGES[code] : GENERIC_MESSAGE;
}
