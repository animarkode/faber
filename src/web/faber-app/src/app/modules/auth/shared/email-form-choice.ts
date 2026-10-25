import { afterNextRender, computed, ElementRef, inject, Injectable, Injector, signal } from '@angular/core';
import { ExternalProvidersStore } from '../external-sign-in/external-providers-store';

/**
 * Whether an auth page shows its provider buttons or its e-mail form.
 * Provide it on the page component: it reads that page's host element to
 * move focus into the form once it replaces the buttons.
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

  /** The view the page opens on, before any choice: providers, or the form when it is all there is. */
  readonly firstView = computed(() => this.settled() && !this.emailChosen());

  showForm(): void {
    this.emailChosen.set(true);
    this.focusFirstField();
  }

  /** The clicked button disappears, so move focus into the form that replaced it. */
  private focusFirstField(): void {
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>('form input')?.focus(), {
      injector: this.injector,
    });
  }
}
