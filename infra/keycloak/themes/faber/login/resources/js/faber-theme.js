// Wires the theme toggle. The initial class is set by the pre-paint script in template.ftl.
(function () {
  'use strict';

  var root = document.documentElement;
  var button = document.getElementById('fb-theme-toggle');
  if (!button) {
    return;
  }

  function sync() {
    var dark = root.classList.contains('dark');
    // The label names the action: in dark mode the button switches back to light.
    button.setAttribute('aria-label', dark ? button.dataset.labelToLight : button.dataset.labelToDark);
  }

  function persist(theme) {
    var cookie = 'faber-theme=' + theme + '; Path=/; SameSite=Lax; Max-Age=31536000';
    if (location.protocol === 'https:') {
      cookie += '; Secure';
    }
    // Production shares the preference with the app across subdomains.
    if (button.dataset.cookieDomain) {
      cookie += '; Domain=' + button.dataset.cookieDomain;
    }
    document.cookie = cookie;
  }

  button.addEventListener('click', function () {
    var dark = root.classList.toggle('dark');
    // PatternFly's own dark palette, for the v2 components faber.css does not restyle.
    root.classList.toggle('pf-v5-theme-dark', dark);
    persist(dark ? 'dark' : 'light');
    sync();
  });

  sync();
})();
