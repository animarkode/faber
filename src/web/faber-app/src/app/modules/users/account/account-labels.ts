const KNOWN_LABELS: Record<string, string> = {
  firstname: 'First name',
  lastname: 'Last name',
  email: 'Email',
  username: 'Username',
  password: 'Password',
  otp: 'Authenticator app',
  totp: 'Authenticator app',
  webauthn: 'Security key',
  'webauthn-passwordless': 'Passkey',
};

/**
 * Turns a Keycloak label — a message key like `${firstName}` or an identifier like `webauthn-passwordless` —
 * into text a person can read.
 */
export function humanize(key: string | null | undefined): string {
  const bare = (key ?? '').trim().replace(/^\$\{(.*)\}$/, '$1').trim();
  if (!bare) return '';
  const known = KNOWN_LABELS[bare.toLowerCase()];
  if (known) return known;
  const words = bare
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[-_.]+/g, ' ')
    .trim()
    .toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}
