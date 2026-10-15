import { Injectable, signal } from '@angular/core';

/**
 * Hands the message for a failed external sign-in from
 * `externalSignInErrorGuard` to the sign-in page exactly once, so the error
 * never has to stay in the URL and a reload does not show it again.
 */
@Injectable({
  providedIn: 'root',
})
export class ExternalSignInErrorStore {
  private readonly pending = signal<string | null>(null);

  set(message: string | null): void {
    this.pending.set(message);
  }

  /** Returns the pending message and forgets it. */
  consume(): string | null {
    const message = this.pending();
    this.pending.set(null);
    return message;
  }
}
