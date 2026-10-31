export const environment = {
  production: false,
  // Same origin as the app: the dev server proxies /api to the API (proxy.conf.json), as the edge proxy
  // does in production, so the session cookie is first-party and SameSite=Strict holds.
  api: '/api/v1',
  // Host-only cookie in dev: localhost:4200 and localhost:8080 share it because cookies are not port-scoped.
  cookieDomain: '',
};
