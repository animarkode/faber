import { afterNextRender, computed, ElementRef, inject, Injectable, Injector, signal } from '@angular/core';
import { ExternalProvidersStore } from '../external-sign-in/external-providers-store';

/** Marks the button that opens the e-mail form, so focus can return to it. */
const EMAIL_OPTION_ATTRIBUTE = 'data-email-option';

/**
 * Whether an auth page shows its provider buttons or its e-mail form.
 * Provide it on the page component: it reads that page's host element to
 * move focus when one view replaces the other.
 */
@Injectable()
export class EmailFormChoice {
  private readonly providersStore = inject(ExternalProvidersStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  /** Set once the user picks e-mail over the provider buttons. */
  private readonly emailChosen = signal(false);

  /** False until the providers request finishes; the page shows neither view meanwhile. */
  readonly settled = this.providersStore.settled;

  /**
   * The form shows straight away when there is no provider to offer;
   * otherwise only after the user asks for it, so the providers stay in view.
   */
  readonly formShown = computed(
    () => this.settled() && (!this.providersStore.hasKnownProviders() || this.emailChosen()),
  );

  /** A way back exists only when there are providers to go back to. */
  readonly canGoBack = computed(() => this.formShown() && this.providersStore.hasKnownProviders());

  showForm(): void {
    this.emailChosen.set(true);
    this.focusAfterRender('form input');
  }

  showOptions(): void {
    this.emailChosen.set(false);
    this.focusAfterRender(`[${EMAIL_OPTION_ATTRIBUTE}]`);
  }

  /** The clicked button disappears, so move focus to the view that replaced it. */
  private focusAfterRender(selector: string): void {
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(selector)?.focus(), {
      injector: this.injector,
    });
  }
}
