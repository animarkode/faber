export type SocialProvider = 'google' | 'github';

export interface SocialProviderEntry {
  id: SocialProvider;
  label: string;
  /** Brand asset path, relative to the app's asset root. */
  icon: string;
  /** Optional asset used instead of `icon` on dark themes. */
  iconDark?: string;
}

export const SOCIAL_PROVIDERS: readonly SocialProviderEntry[] = [
  { id: 'google', label: 'Google', icon: 'brands/google.svg' },
  {
    id: 'github',
    label: 'GitHub',
    icon: 'brands/github-mark.svg',
    iconDark: 'brands/github-mark-white.svg',
  },
];

export function isKnownProvider(alias: string): alias is SocialProvider {
  return SOCIAL_PROVIDERS.some((provider) => provider.id === alias);
}
