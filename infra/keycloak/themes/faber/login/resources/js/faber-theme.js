// Wires the shell. The initial theme class is set by the pre-paint script in template.ftl; the
// toggle's icon and accessible name follow html.dark through faber.css, so this only flips and saves.
(function () {
  'use strict';

  var root = document.documentElement;

  // Host-only: Keycloak's pages are served on the web app's host, so both read the same cookie.
  function persist(theme) {
    var cookie = 'faber-theme=' + theme + '; Path=/; SameSite=Lax; Max-Age=31536000';
    if (location.protocol === 'https:') {
      cookie += '; Secure';
    }
    document.cookie = cookie;
  }

  var button = document.getElementById('fb-theme-toggle');
  if (button) {
    button.addEventListener('click', function () {
      var dark = root.classList.toggle('dark');
      // PatternFly's own dark palette, for the v2 components faber.css does not restyle.
      root.classList.toggle('pf-v5-theme-dark', dark);
      persist(dark ? 'dark' : 'light');
    });
  }

  // Sign-in: v2 marks only the email field invalid for a wrong email/password pair. The alert above
  // the form carries that error now, so tell assistive tech the field itself is fine, as it looks.
  if (document.body.getAttribute('data-page-id') === 'login-login') {
    var username = document.getElementById('username');
    if (username) {
      username.removeAttribute('aria-invalid');
      username.removeAttribute('aria-describedby');
    }
  }

  // A spinner on the primary button while the next page loads. Only after 300ms, so a fast reply never
  // flashes it. Keycloak forms post and navigate, so nothing ever clears it on success.
  document.addEventListener('submit', function (event) {
    var button = event.submitter;
    if (!button || !button.classList.contains('pf-m-primary')) {
      return;
    }
    // v2's onsubmit disables the button at once; keep it full strength so it does not dim, then spin.
    button.classList.add('fb-submitting');
    setTimeout(function () {
      if (event.defaultPrevented) {
        return;
      }
      // Drawn in the label's colour, read before .fb-busy hides the label.
      var svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'>" +
        "<circle cx='12' cy='12' r='9' fill='none' stroke='" + getComputedStyle(button).color + "' stroke-width='2.5' stroke-linecap='round' stroke-dasharray='42 15'>" +
        "<animateTransform attributeName='transform' type='rotate' from='0 12 12' to='360 12 12' dur='0.8s' repeatCount='indefinite'/>" +
        '</circle></svg>';
      button.style.setProperty('--fb-spinner', 'url("data:image/svg+xml,' + encodeURIComponent(svg) + '")');
      button.classList.add('fb-busy');
      button.setAttribute('aria-busy', 'true');
    }, 300);
  });

  // Back/forward restores the page from the browser cache as it was left: spinning, and disabled by
  // v2's own onsubmit. Put the buttons back so the form can be sent again.
  window.addEventListener('pageshow', function (event) {
    if (!event.persisted) {
      return;
    }
    document.querySelectorAll('.pf-v5-c-button.pf-m-primary').forEach(function (button) {
      button.classList.remove('fb-submitting', 'fb-busy');
      button.style.removeProperty('--fb-spinner');
      button.removeAttribute('aria-busy');
      button.disabled = false;
    });
  });

  // Like the app's navbar: transparent at the top, frosted glass once the page scrolls.
  var header = document.getElementById('fb-header');
  if (header) {
    var update = function () {
      header.classList.toggle('fb-header--glass', window.scrollY > 10);
    };
    window.addEventListener('scroll', update, { passive: true });
    update();
  }
})();
