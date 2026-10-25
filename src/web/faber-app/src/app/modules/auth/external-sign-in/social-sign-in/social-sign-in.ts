import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { NgOptimizedImage, NgTemplateOutlet } from '@angular/common';
import { API_ROUTES } from '../../../../core/routes/api-routes';
import { ExternalProvidersStore } from '../external-providers-store';
import { isKnownProvider, SOCIAL_PROVIDERS, SocialProvider } from '../social-providers';

interface SocialProviderLink {
  id: SocialProvider;
  text: string;
  href: string;
  icon: string;
  iconDark?: string;
}

@Component({
  selector: 'app-social-sign-in',
  imports: [NgOptimizedImage, NgTemplateOutlet],
  templateUrl: './social-sign-in.html',
  styleUrl: './social-sign-in.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SocialSignIn {
  private readonly providersStore = inject(ExternalProvidersStore);

  /**
   * Where the "or" divider goes: `before` when the buttons follow another way
   * of signing in, `after` when they lead and another option follows.
   */
  readonly divider = input<'before' | 'after'>('before');

  /** Catalog entries the API reports as available; empty while loading or on error. */
  protected readonly links = computed<SocialProviderLink[]>(() => {
    const available = new Set(this.providersStore.providers().filter(isKnownProvider));
    return SOCIAL_PROVIDERS.filter((provider) => available.has(provider.id)).map((provider) => ({
      id: provider.id,
      text: `Continue with ${provider.label}`,
      href: API_ROUTES.auth.external.start(provider.id),
      icon: provider.icon,
      iconDark: provider.iconDark,
    }));
  });
}
