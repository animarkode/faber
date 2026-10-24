import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'fb-auth-card',
  templateUrl: './fb-auth-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    role: 'region',
    class:
      'relative block w-[min(100%,26rem)] rounded-[2rem] bg-card [--icon-button-surface:var(--color-muted)] p-[clamp(1.25rem,3vw,1.5rem)] max-sm:flex max-sm:min-h-[100dvh] max-sm:w-full max-sm:flex-col max-sm:justify-center max-sm:rounded-none max-sm:px-6 max-sm:py-8',
  },
})
export class FbAuthCard {}
