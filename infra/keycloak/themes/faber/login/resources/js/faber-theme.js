// Wires the theme toggle. The initial class is set by the pre-paint script in template.ftl; the
// button's icon and accessible name follow html.dark through faber.css, so this only flips and saves.
(function () {
  'use strict';

  var root = document.documentElement;
  var button = document.getElementById('fb-theme-toggle');
  if (!button) {
    return;
  }

  // Host-only: Keycloak's pages are served on the web app's host, so both read the same cookie.
  function persist(theme) {
    var cookie = 'faber-theme=' + theme + '; Path=/; SameSite=Lax; Max-Age=31536000';
    if (location.protocol === 'https:') {
      cookie += '; Secure';
    }
    document.cookie = cookie;
  }

  button.addEventListener('click', function () {
    var dark = root.classList.toggle('dark');
    // PatternFly's own dark palette, for the v2 components faber.css does not restyle.
    root.classList.toggle('pf-v5-theme-dark', dark);
    persist(dark ? 'dark' : 'light');
  });
})();
