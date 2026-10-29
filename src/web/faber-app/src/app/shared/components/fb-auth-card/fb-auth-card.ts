import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'fb-auth-card',
  templateUrl: './fb-auth-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    role: 'region',
    class:
      // No card fill: the form sits on the page background, so fields take
      // the card colour to stand out from it.
      'relative block w-[min(100%,26rem)] [--color-input:var(--color-card)] p-[clamp(1.25rem,3vw,1.5rem)] max-sm:w-full max-sm:px-4',
  },
})
export class FbAuthCard {}
