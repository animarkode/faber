import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NgOptimizedImage } from '@angular/common';
import { API_ROUTES } from '../../../../core/routes/api-routes';
import { ExternalProvidersStore } from '../external-providers-store';
import { isKnownProvider, SOCIAL_PROVIDERS, SocialProvider } from '../social-providers';

interface SocialProviderLink {
  id: SocialProvider;
  text: string;
  href: string;
  icon: string;
  iconDark?: string;
  /** Brand-specific surface, border and text colours. */
  classes: string;
}

const GOOGLE_CLASSES =
  'border-[#747775] bg-white text-[#1F1F1F] dark:border-[#8E918F] dark:bg-[#131314] dark:text-[#E3E3E3]';
const NEUTRAL_CLASSES = 'border-line bg-background text-foreground';

@Component({
  selector: 'app-social-sign-in',
  imports: [NgOptimizedImage],
  templateUrl: './social-sign-in.html',
  styleUrl: './social-sign-in.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SocialSignIn {
  private readonly providersStore = inject(ExternalProvidersStore);

  /** Catalog entries the API reports as available; empty while loading or on error. */
  protected readonly links = computed<SocialProviderLink[]>(() => {
    const available = new Set(this.providersStore.providers().filter(isKnownProvider));
    return SOCIAL_PROVIDERS.filter((provider) => available.has(provider.id)).map((provider) => ({
      id: provider.id,
      text: `Continue with ${provider.label}`,
      href: API_ROUTES.auth.external.start(provider.id),
      icon: provider.icon,
      iconDark: provider.iconDark,
      classes: provider.id === 'google' ? GOOGLE_CLASSES : NEUTRAL_CLASSES,
    }));
  });
}
