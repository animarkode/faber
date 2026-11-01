<#-- Page illustrations, ported from the design artboards. Decorative: always aria-hidden.
     Animations are one-shot and switched off under prefers-reduced-motion (faber.css). -->

<#macro envelope>
<div class="fb-envelope" aria-hidden="true">
  <svg width="120" height="100" viewBox="0 0 120 100" focusable="false">
    <rect x="12" y="40" width="96" height="56" rx="10" style="fill: var(--color-surface)"></rect>
    <g class="fb-envelope__letter">
      <rect x="28" y="6" width="64" height="60" rx="8" style="fill: var(--color-primary)"></rect>
      <rect x="38" y="18" width="44" height="5" rx="2.5" style="fill: var(--color-primary-foreground)"></rect>
      <rect x="38" y="29" width="32" height="5" rx="2.5" style="fill: var(--color-primary-foreground); opacity: 0.55"></rect>
    </g>
    <path d="M12 50 L60 76 L108 50 V86 a10 10 0 0 1 -10 10 H22 a10 10 0 0 1 -10 -10 Z" style="fill: var(--color-card); stroke: var(--color-field-border); stroke-width: 1.5; stroke-linejoin: round"></path>
  </svg>
</div>
</#macro>

<#-- One Lucide glyph inside a tile; x is the tile's left offset (14 on the left tile, 110 on the right). -->
<#macro glyph name x>
  <#if name = "github">
    <path transform="translate(${x} 36) scale(1.17)" d="M12 .5C5.65.5.5 5.65.5 12c0 5.08 3.29 9.39 7.86 10.91.58.1.79-.25.79-.56v-2c-3.2.7-3.87-1.37-3.87-1.37-.52-1.33-1.28-1.69-1.28-1.69-1.05-.72.08-.7.08-.7 1.16.08 1.77 1.19 1.77 1.19 1.03 1.77 2.7 1.26 3.36.96.1-.75.4-1.26.73-1.55-2.55-.29-5.24-1.28-5.24-5.69 0-1.26.45-2.29 1.19-3.1-.12-.29-.52-1.46.11-3.05 0 0 .97-.31 3.17 1.18a11 11 0 0 1 5.77 0c2.2-1.49 3.17-1.18 3.17-1.18.63 1.59.23 2.76.11 3.05.74.81 1.19 1.84 1.19 3.1 0 4.42-2.7 5.39-5.26 5.68.41.36.78 1.06.78 2.14v3.17c0 .31.21.67.8.56A11.5 11.5 0 0 0 23.5 12C23.5 5.65 18.35.5 12 .5z" style="fill: var(--color-foreground)"></path>
  <#elseif name = "person-solid">
    <circle cx="36" cy="43" r="8" style="fill: var(--color-foreground)"></circle>
    <path d="M22 66a14 12 0 0 1 28 0Z" style="fill: var(--color-foreground)"></path>
  <#else>
    <g transform="translate(${x} 36) scale(1.17)" style="fill: none; stroke: var(--color-foreground); stroke-width: 2; stroke-linecap: round; stroke-linejoin: round">
      <#if name = "laptop">
        <path d="M18 5a2 2 0 0 1 2 2v8.526a2 2 0 0 0 .212.897l1.068 2.127a1 1 0 0 1-.9 1.45H3.62a1 1 0 0 1-.9-1.45l1.068-2.127A2 2 0 0 0 4 15.526V7a2 2 0 0 1 2-2z"></path><path d="M20.054 15.987H3.946"></path>
      <#elseif name = "key">
        <path d="M2.586 17.414A2 2 0 0 0 2 18.828V21a1 1 0 0 0 1 1h3a1 1 0 0 0 1-1v-1a1 1 0 0 1 1-1h1a1 1 0 0 0 1-1v-1a1 1 0 0 1 1-1h.172a2 2 0 0 0 1.414-.586l.814-.814a6.5 6.5 0 1 0-4-4z"></path><circle cx="16.5" cy="7.5" r=".5"></circle>
      <#elseif name = "person">
        <circle cx="12" cy="8" r="5"></circle><path d="M20 21a8 8 0 0 0-16 0"></path>
      <#elseif name = "mail">
        <rect width="20" height="16" x="2" y="4" rx="2"></rect><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"></path>
      <#elseif name = "clock">
        <circle cx="12" cy="12" r="10"></circle><path d="M12 6v6l4 2"></path>
      <#else>
        <path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"></path><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"></path>
      </#if>
    </g>
  </#if>
</#macro>

<#-- Two tiles joined by a yellow bridge. bridge: link | check | cross | dashed | dots -->
<#macro linkart left right bridge="link">
<div class="fb-linkart" aria-hidden="true">
  <svg width="160" height="100" viewBox="0 0 160 100" focusable="false">
    <#if bridge = "link">
      <rect class="fb-linkart__bridge" x="52" y="44" width="56" height="12" rx="6" style="fill: var(--color-primary)"></rect>
    <#elseif bridge = "check">
      <rect x="52" y="44" width="56" height="12" rx="6" style="fill: var(--color-primary)"></rect>
      <g class="fb-linkart__cross" style="fill: none; stroke: var(--color-success-ink); stroke-width: 4.5; stroke-linecap: round; stroke-linejoin: round"><path d="M70 50 L77 57 L91 43"></path></g>
    <#elseif bridge = "cross">
      <rect x="52" y="44" width="56" height="12" rx="6" style="fill: var(--color-primary)"></rect>
      <g class="fb-linkart__cross" style="stroke: var(--color-destructive-text); stroke-width: 4.5; stroke-linecap: round"><path d="M71 41 L89 59"></path><path d="M89 41 L71 59"></path></g>
    <#elseif bridge = "dashed">
      <g class="fb-linkart__bridge" style="fill: var(--color-primary)"><rect x="66" y="44" width="8" height="12" rx="4"></rect><rect x="78" y="44" width="8" height="12" rx="4"></rect><rect x="90" y="44" width="8" height="12" rx="4"></rect></g>
    <#elseif bridge = "dots">
      <g class="fb-linkart__cross" style="fill: var(--color-muted-foreground)"><circle cx="72" cy="50" r="3"></circle><circle cx="80" cy="50" r="3"></circle><circle cx="88" cy="50" r="3"></circle></g>
    </#if>
    <g class="fb-linkart__left">
      <rect x="8" y="22" width="56" height="56" rx="16" style="fill: var(--color-card); stroke: var(--color-field-border); stroke-width: 1.5"></rect>
      <@glyph left 22 />
    </g>
    <g class="fb-linkart__right">
      <rect x="96" y="22" width="56" height="56" rx="16" style="fill: var(--color-card); stroke: var(--color-field-border); stroke-width: 1.5"></rect>
      <@glyph right 110 />
    </g>
  </svg>
</div>
</#macro>
