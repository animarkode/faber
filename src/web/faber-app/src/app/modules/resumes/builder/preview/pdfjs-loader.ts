import { InjectionToken } from '@angular/core';
import type * as PdfJs from 'pdfjs-dist';

export type PdfJsLoader = () => Promise<typeof PdfJs>;

/**
 * Loads PDF.js together with its worker. The worker is imported through the
 * bundler's `file` loader, so the build emits it as a hashed asset and hands
 * back its URL — it cannot be missing from the output, and it always comes
 * from the same `pdfjs-dist` version as the library itself.
 */
async function loadPdfJsWithWorker(): Promise<typeof PdfJs> {
  const [lib, worker] = await Promise.all([
    import('pdfjs-dist'),
    import('pdfjs-dist/build/pdf.worker.min.mjs', { with: { loader: 'file' } }),
  ]);
  lib.GlobalWorkerOptions.workerSrc = new URL(
    worker.default,
    document.baseURI,
  ).toString();
  return lib;
}

/** Browser loader kept behind DI so preview tests never execute PDF.js browser globals. */
export const PDFJS_LOADER = new InjectionToken<PdfJsLoader>('PDF.js loader', {
  providedIn: 'root',
  factory: () => loadPdfJsWithWorker,
});
