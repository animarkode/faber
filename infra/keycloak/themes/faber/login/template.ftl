<#import "field.ftl" as field>
<#import "footer.ftl" as loginFooter>
<#import "theme-resources.ftl" as themeResourceTags>
<#import "fb-illustrations.ftl" as fbArt>
<#macro username>
  <#assign label>
    <#if !realm.loginWithEmailAllowed>${msg("username")}<#elseif !realm.registrationEmailAsUsername>${msg("usernameOrEmail")}<#else>${msg("email")}</#if>
  </#assign>
  <@field.group name="username" label=label>
    <div class="${properties.kcInputGroup}">
      <div class="${properties.kcInputGroupItemClass} ${properties.kcFill}">
        <span class="${properties.kcInputClass} ${properties.kcFormReadOnlyClass}">
          <input id="kc-attempted-username" value="${auth.attemptedUsername}" readonly>
        </span>
      </div>
      <div class="${properties.kcInputGroupItemClass}">
        <button id="reset-login" class="${properties.kcFormPasswordVisibilityButtonClass} kc-login-tooltip" type="button" 
              aria-label="${msg('restartLoginTooltip')}" onclick="location.href='${url.loginRestartFlowUrl}'">
            <i class="fa-sync-alt fas" aria-hidden="true"></i>
            <span class="kc-tooltip-text">${msg("restartLoginTooltip")}</span>
        </button>
      </div>
    </div>
  </@field.group>
</#macro>

<#macro registrationLayout bodyClass="" displayInfo=false displayMessage=true displayRequiredFields=false>
<!DOCTYPE html>
<html class="${properties.kcHtmlClass!}" lang="${lang}"<#if realm.internationalizationEnabled> dir="${(locale.rtl)?then('rtl','ltr')}"</#if>>

<head>
    <meta charset="utf-8">
    <meta http-equiv="Content-Type" content="text/html; charset=UTF-8" />
    <meta name="color-scheme" content="light${darkMode?then(' dark', '')}">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <#-- Fetch the faces the page shows straight away before the first paint. faber.css uses
         font-display: optional, so a font that is late is skipped instead of swapped in mid-page. -->
    <#assign fbFonts = ["nunito-sans-latin", "playfair-display-latin", "space-grotesk-latin"]>
    <#if social?? && social.providers?has_content>
      <#assign fbFonts = fbFonts + ["google-sans-latin"]>
    </#if>
    <#list fbFonts as fbFont>
    <link rel="preload" href="${url.resourcesPath}/fonts/${fbFont}.woff2" as="font" type="font/woff2" crossorigin>
    </#list>
    <#-- Pre-paint theme: the faber-theme cookie wins, else the OS preference. Runs before the CSS so there is no flash. -->
    <script>
      (function () {
        var m = document.cookie.match(/(?:^|; )faber-theme=(dark|light)/);
        if (m ? m[1] === 'dark' : window.matchMedia('(prefers-color-scheme: dark)').matches) {
          document.documentElement.classList.add('dark', 'pf-v5-theme-dark');
        }
      })();
    </script>

    <#if properties.meta?has_content>
        <#list properties.meta?split(' ') as meta>
            <meta name="${meta?split('==')[0]}" content="${meta?split('==')[1]}"/>
        </#list>
    </#if>
    <title>${title!}</title>
    <#if themeResources?? && themeResources.favicons?has_content>
        <@themeResourceTags.renderFavicons themeResources.favicons url.resourcesPath />
    <#else>
        <link rel="icon" href="${url.resourcesPath}/img/favicon.ico" />
    </#if>
    <#if themeResources?? && themeResources.stylesCommon?has_content>
        <@themeResourceTags.renderStyles themeResources.stylesCommon url.resourcesCommonPath />
    <#elseif properties.stylesCommon?has_content>
        <#list properties.stylesCommon?split(' ') as style>
            <link href="${url.resourcesCommonPath}/${style}" rel="stylesheet" />
        </#list>
    </#if>
    <#if themeResources?? && themeResources.styles?has_content>
        <@themeResourceTags.renderStyles themeResources.styles url.resourcesPath />
    <#elseif properties.styles?has_content>
        <#list properties.styles?split(' ') as style>
            <link href="${url.resourcesPath}/${style}" rel="stylesheet" />
        </#list>
    </#if>
    <script type="importmap">
        {
            "imports": {
                "rfc4648": "${url.resourcesCommonPath}/vendor/rfc4648/rfc4648.js"
            }
        }
    </script>
    <#if darkMode>
      <script type="module" async blocking="render">
          <#outputformat "JavaScript">
          const DARK_MODE_CLASS = ${properties.kcDarkModeClass?c};
          const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");

          updateDarkMode(mediaQuery.matches);
          mediaQuery.addEventListener("change", (event) => updateDarkMode(event.matches));

          function updateDarkMode(isEnabled) {
            const { classList } = document.documentElement;

            if (isEnabled) {
              classList.add(DARK_MODE_CLASS);
            } else {
              classList.remove(DARK_MODE_CLASS);
            }
          }
          </#outputformat>
      </script>
    </#if>
    <#if themeResources?? && themeResources.scripts?has_content>
        <@themeResourceTags.renderScripts themeResources.scripts url.resourcesPath "text/javascript" />
    <#elseif properties.scripts?has_content>
        <#list properties.scripts?split(' ') as script>
            <script src="${url.resourcesPath}/${script}" type="text/javascript"></script>
        </#list>
    </#if>
    <#if scripts??>
        <#list scripts as script>
            <script src="${script}" type="text/javascript"></script>
        </#list>
    </#if>
    <script type="module" src="${url.resourcesPath}/js/passwordVisibility.js"></script>
    <script src="${url.resourcesPath}/js/faber-theme.js" defer></script>
    <script type="module">
        <#outputformat "JavaScript">
        import { startSessionPolling } from ${(url.resourcesPath + "/js/authChecker.js")?c};

        startSessionPolling(
            ${url.ssoLoginInOtherTabsUrl?c}
        );
        </#outputformat>
    </script>
    <script type="module">
        document.addEventListener("click", (event) => {
            const link = event.target.closest("a[data-once-link]");

            if (!link) {
                return;
            }

            if (link.getAttribute("aria-disabled") === "true") {
                event.preventDefault();
                return;
            }

            const { disabledClass } = link.dataset;

            if (disabledClass) {
                link.classList.add(...disabledClass.trim().split(/\s+/));
            }

            link.setAttribute("role", "link");
            link.setAttribute("aria-disabled", "true");
        });
    </script>
    <#if authenticationSession??>
        <script type="module">
             <#outputformat "JavaScript">
            import { checkAuthSession } from ${(url.resourcesPath + "/js/authChecker.js")?c};

            checkAuthSession(
                ${authenticationSession.authSessionIdHash?c}
            );
            </#outputformat>
        </script>
    </#if>
    <script>
      // Workaround for https://bugzilla.mozilla.org/show_bug.cgi?id=1404468
      const isFirefox = true;
    </script>
</head>

<body id="keycloak-bg" class="${properties.kcBodyClass!}" data-page-id="login-${pageId}">
<#-- Shell: the web app's navbar, brand and theme toggle only. The brand leads back to the app's
     home page (the client's base URL); a page without a client shows it as plain text. -->
<header class="fb-header" id="fb-header">
  <div class="fb-header__inner">
      <#if (client.baseUrl)?has_content>
      <a class="fb-brand" href="${client.baseUrl}">microcivi.com</a>
      <#else>
      <span class="fb-brand">microcivi.com</span>
      </#if>
      <#-- Both labels are rendered; faber.css hides the one that does not fit html.dark, and a
           display:none element drops out of the accessibility tree, so the name is right before any JS runs. -->
      <button type="button" id="fb-theme-toggle" class="fb-icon-btn">
        <span class="fb-sr-only fb-label-to-dark">${msg("fbSwitchToDark")}</span>
        <span class="fb-sr-only fb-label-to-light">${msg("fbSwitchToLight")}</span>
        <svg class="fb-icon-moon" xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M20.985 12.486a9 9 0 1 1-9.473-9.472c.405-.022.617.46.402.803a6 6 0 0 0 8.268 8.268c.344-.215.825-.004.803.401"/></svg>
        <svg class="fb-icon-sun" xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="4"/><path d="M12 2v2"/><path d="M12 20v2"/><path d="m4.93 4.93 1.41 1.41"/><path d="m17.66 17.66 1.41 1.41"/><path d="M2 12h2"/><path d="M20 12h2"/><path d="m6.34 17.66-1.41 1.41"/><path d="m19.07 4.93-1.41 1.41"/></svg>
      </button>
  </div>
</header>
<div class="${properties.kcLogin!}">
  <div class="${properties.kcLoginContainer!}">
    <main class="${properties.kcLoginMain!}">
      <div class="${properties.kcLoginMainHeader!}">
        <#-- Illustration above the title, picked from the page. Pages not listed have none. -->
        <#switch pageId!"">
          <#case "login-verify-email">
            <@fbArt.envelope />
            <#break>
          <#case "webauthn-authenticate">
            <@fbArt.linkart left="laptop" right="key" />
            <#break>
          <#case "webauthn-register">
            <@fbArt.linkart left="person" right="key" />
            <#break>
          <#case "webauthn-error">
            <@fbArt.linkart left="laptop" right="key" bridge="cross" />
            <#break>
          <#case "login-idp-link-confirm">
            <@fbArt.linkart left="person-solid" right=((idpAlias!"") = "github")?then("github", "link") />
            <#break>
          <#case "info">
            <#-- messageHeader arrives as the resolved text, not the key, so compare it with msg(key).
                 Keycloak sends the not-yet-confirmed page as "success" too; it gets no check yet. -->
            <#if (messageHeader!"") = msg("confirmEmailAddressVerificationHeader")>
              <@fbArt.linkart left="mail" right="person" />
            <#elseif (message.type)! = "success" || (messageHeader!"") = msg("emailVerifiedMessageHeader") || (messageHeader!"") = msg("emailVerifiedAlreadyMessageHeader")>
              <@fbArt.linkart left="mail" right="person" bridge="check" />
            </#if>
            <#break>
          <#case "error">
            <@fbArt.linkart left="mail" right="key" bridge="cross" />
            <#break>
          <#case "login-page-expired">
            <@fbArt.linkart left="person" right="clock" bridge="dashed" />
            <#break>
          <#case "logout-confirm">
            <@fbArt.linkart left="person" right="laptop" />
            <#break>
        </#switch>
        <#-- error.ftl titles every error "Something went wrong"; an expired e-mail link gets its own title. -->
        <#assign fbExpiredLink = (pageId!"") = "error" && message?has_content
          && (message.summary = msg("expiredActionTokenNoSessionMessage") || message.summary = msg("expiredActionTokenSessionExistsMessage"))>
        <h1 class="${properties.kcLoginMainTitle!}" id="kc-page-title"><#if fbExpiredLink>${msg("fbExpiredLinkTitle")}<#else><#nested "header"></#if></h1>
        <#assign fbSubtitleKeys = {
          "login": "fbSubtitleLogin",
          "register": "fbSubtitleRegister",
          "login-update-password": "fbSubtitleUpdatePassword",
          "webauthn-authenticate": "fbSubtitlePasskeyAuthenticate",
          "webauthn-register": "fbSubtitlePasskeyRegister",
          "webauthn-error": "fbSubtitlePasskeyError",
          "login-idp-link-confirm": "fbSubtitleLinkAccount",
          "login-page-expired": "fbSubtitlePageExpired",
          "update-email": "fbSubtitleUpdateEmail"
        }>
        <#if fbSubtitleKeys[pageId!""]??>
          <p class="fb-lead">${msg(fbSubtitleKeys[pageId])}</p>
        <#elseif (pageId!"") = "login-reset-password">
          <#-- v2 prints this in the footer; here it moves under the title and faber.css hides the footer copy -->
          <p class="fb-lead">${realm.duplicateEmailsAllowed?then(msg("emailInstructionUsername"), msg("emailInstruction"))}</p>
        </#if>
        <#-- Email pages: the instruction is plain text under the title and the address is a badge under it.
             The stock sentence that runs both together is hidden in faber.css (.fb-own-instruction). -->
        <#assign fbEmail = "">
        <#if (pageId!"") = "login-verify-email" && verifyEmail??>
          <#assign fbEmail = verifyEmail>
          <p class="fb-lead">${msg("fbVerifyEmailLead")}</p>
        <#elseif (pageId!"") = "info" && (messageHeader!"") = msg("confirmEmailAddressVerificationHeader") && message?has_content>
          <#-- The address only arrives inside the formatted summary; cut the message's own words off it. -->
          <#assign fbParts = msg("confirmEmailAddressVerification", "@@fb@@")?split("@@fb@@")>
          <#assign fbEmail = message.summary?remove_beginning(fbParts[0])?remove_ending(fbParts[1])>
          <p class="fb-lead">${msg("fbConfirmEmailLead")}</p>
        <#elseif (pageId!"") = "info" && !(messageHeader??) && message?has_content && message.summary = msg("successLogout")>
          <#-- info.ftl prints the same sentence as title and body; the body becomes this lead instead. -->
          <#assign fbOwnInstruction = true>
          <p class="fb-lead">${msg("fbSignedOutLead")}</p>
        </#if>
        <#if fbEmail?has_content>
          <p class="fb-identity">
            <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m22 7-8.991 5.727a2 2 0 0 1-2.009 0L2 7"/><rect x="2" y="4" width="20" height="16" rx="2"/></svg>
            <span>${fbEmail}</span>
          </p>
        </#if>
      </div>
      <div class="${properties.kcLoginMainBody!}${(fbEmail?has_content || (fbOwnInstruction!false))?then(" fb-own-instruction", "")}">
        <#-- v2 prints the required-fields note at the top, above the provider buttons it does not apply to;
             here it sits right above the form (see below). -->
        <#if auth?has_content && auth.showUsername() && !auth.showResetCredentials()>
            <div class="${properties.kcFormClass} ${properties.kcContentWrapperClass}">
              <#nested "show-username">
              <@username />
            </div>
        </#if>

        <#-- App-initiated actions should not see warning messages about the need to complete the action -->
        <#-- during login.                                                                               -->
        <#-- login.ftl turns the message off when the email/password pair is wrong and shows it under the email
             field instead. It is about the pair, not that field, so here it is the alert above the form;
             faber.css hides the field copy. -->
        <#assign fbPairError = (pageId!"") = "login" && messagesPerField?? && messagesPerField.existsError('username','password')>
        <#-- Required-action pages open with Keycloak's "You need to <do this page's job> to activate your
             account." It only repeats the title and subtitle, so it is dropped; any other warning still shows. -->
        <#assign fbRepeatsTitle = false>
        <#if message?has_content && message.type = 'warning'>
          <#list ["verifyEmailMessage", "updatePasswordMessage", "resetPasswordMessage", "updateProfileMessage",
                  "updateEmailMessage", "configureTotpMessage", "configureBackupCodesMessage"] as fbKey>
            <#if message.summary = msg(fbKey)><#assign fbRepeatsTitle = true></#if>
          </#list>
        </#if>
        <#if (displayMessage || fbPairError) && message?has_content && !fbRepeatsTitle && (message.type != 'warning' || !isAppInitiatedAction??)>
            <#assign fbAlertType = (message.type = 'error')?then('error', message.type)>
            <div class="fb-alert fb-alert--${fbAlertType}" role="${(message.type = 'error')?then('alert', 'status')}">
                <#if message.type = 'success'>
                    <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="m9 12 2 2 4-4"/></svg>
                <#elseif message.type = 'info'>
                    <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/></svg>
                <#else>
                    <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3"/><path d="M12 9v4"/><path d="M12 17h.01"/></svg>
                </#if>
                <span class="kc-feedback-text">${message.summary}</span>
            </div>
        </#if>

        <#-- Social providers sit above the form, joined to it by an "or" divider. The divider that
             social-providers.ftl renders itself is hidden in faber.css. -->
        <#assign fbSocial><#nested "socialProviders"></#assign>
        <#-- login.ftl etc. emit an HTML comment even for an empty section, so strip comments before testing for content -->
        <#if fbSocial?markup_string?replace("<!--.*?-->", "", "rs")?trim?has_content>
            <div class="fb-social">
                ${fbSocial}
                <div class="fb-divider" aria-hidden="true"><span>${msg("orLabel")}</span></div>
            </div>
        </#if>

        <#if displayRequiredFields>
            <p class="fb-required-note"><span class="${properties.kcInputRequiredClass!}">*</span> ${msg("fbRequiredFieldsNote")}</p>
        </#if>

        <#nested "form">

        <#-- Page expired: v2 offers "restart" and "continue" as two "click here" links. From the usual cause
             (Back on the sign-in page) both land on the same form; mid-flow, continue keeps the steps already
             done and restart drops them. So one button, continue; faber.css hides the stock sentence. -->
        <#-- Keycloak drops the link (skipLink) on pages like "You are logged out" after a sign-out that did
             not start in the app; its only client there is the account console. That left a dead end, so the
             page links to the app instead (fbAppUrl in theme.properties). -->
        <#if (pageId!"") = "info" && (skipLink!false)>
            <a id="fb-back-to-app" class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!}" href="${properties.fbAppUrl!"/"}">${msg("backToApplication")}</a>
        </#if>

        <#if (pageId!"") = "login-page-expired">
            <a class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!}" href="${url.loginAction}">${msg("doContinue")}</a>
        </#if>

        <#if auth?has_content && auth.showTryAnotherWayLink()>
          <form id="kc-select-try-another-way-form" action="${url.loginAction}" method="post" novalidate="novalidate">
              <input type="hidden" name="tryAnotherWay" value="on"/>
              <a id="try-another-way" href="javascript:document.forms['kc-select-try-another-way-form'].requestSubmit()"
                  class="${properties.kcButtonSecondaryClass} ${properties.kcButtonBlockClass} ${properties.kcMarginTopClass}">
                    ${msg("doTryAnotherWay")}
              </a>
          </form>
        </#if>

        <#if switchOrganizationEnabled?? && switchOrganizationEnabled>
          <form id="kc-switch-organization-form" action="${url.loginAction}" method="post" novalidate="novalidate">
              <input type="hidden" name="switchOrganization" value="true"/>
              <a id="switch-organization" href="javascript:document.forms['kc-switch-organization-form'].requestSubmit()"
                  class="${properties.kcButtonSecondaryClass} ${properties.kcButtonBlockClass} ${properties.kcMarginTopClass}">
                    ${msg("doSwitchOrganization")}
              </a>
          </form>
        </#if>

          <div class="${properties.kcLoginMainFooter!}">
              <#if displayInfo>
                  <div id="kc-info" class="${properties.kcLoginMainFooterBand!} ${properties.kcFormClass}">
                      <div id="kc-info-wrapper" class="${properties.kcLoginMainFooterBandItem!}">
                          <#nested "info">
                      </div>
                  </div>
              </#if>
          </div>
      </div>

        <div class="${properties.kcLoginMainFooter!}">
            <@loginFooter.content/>
        </div>
    </main>
  </div>
</div>
</body>
</html>
</#macro>
