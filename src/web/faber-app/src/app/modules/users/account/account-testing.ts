import { vi } from 'vitest';
import { of } from 'rxjs';

/** A confirm that answers immediately, for section specs. */
export function confirmAnswering(answer: boolean) {
  return { ask: vi.fn(() => of(answer)) };
}
