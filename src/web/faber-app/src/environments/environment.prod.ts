export const environment = {
  production: true,
  api: '/api/v1',
  // Parent domain shared by the app and Keycloak subdomains (e.g. 'example.com'), so the theme cookie is
  // visible to both. Must equal the login theme's FABER_COOKIE_DOMAIN. Empty = host-only cookie.
  cookieDomain: '',
};
