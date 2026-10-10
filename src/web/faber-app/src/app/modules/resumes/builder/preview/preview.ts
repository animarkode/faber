import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  input,
  OnDestroy,
  signal,
  viewChild,
} from '@angular/core';
import type * as PdfJs from 'pdfjs-dist';
import { LucideAngularModule } from 'lucide-angular';
import { ResumesStore } from '../../resumes-store';
import { PDFJS_LOADER } from './pdfjs-loader';

@Component({
  selector: 'app-preview',
  imports: [LucideAngularModule],
  templateUrl: './preview.html',
  styleUrl: './preview.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block h-full min-h-0' },
})
export class Preview implements OnDestroy {
  protected readonly store = inject(ResumesStore);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly pdfjsLoader = inject(PDFJS_LOADER);
  private readonly canvasA = viewChild<ElementRef<HTMLCanvasElement>>('canvasA');
  private readonly canvasB = viewChild<ElementRef<HTMLCanvasElement>>('canvasB');
  private readonly frame = viewChild<ElementRef<HTMLDivElement>>('frame');
  private readonly paper = viewChild<ElementRef<HTMLDivElement>>('paper');

  /**
   * Whether the card pins a min-width so the desktop 50/50 row can never squeeze
   * the page. False when the preview is the only pane (mobile, #425): the lock
   * would demand roughly `cardHeight × pageAspect` — wider than a 375px viewport
   * — and force horizontal overflow.
   */
  readonly lockWidth = input(true);

  readonly currentPage = signal(1);
  readonly totalPages = signal(0);
  readonly scrollable = signal(true);

  /**
   * Which of the two stacked canvases is currently on screen. Every render
   * targets the *other* one (see `backCanvas`) and only flips this once that
   * render has fully resolved — the canvas named here is the one guaranteed
   * to never be cleared or resized mid-render.
   */
  readonly frontBuffer = signal<'a' | 'b'>('a');

  /**
   * The single, persistent announcement for the first-load skeleton (#456 §1).
   * Empty whenever the skeleton isn't showing, so only the "now generating"
   * transition is ever read out — mirrors `BuilderActions.saveAnnouncement`
   * (builder-actions.ts), which the same persistent-live-region pattern in
   * preview.html depends on: a region that already exists in the DOM before its
   * text changes is what a screen reader actually announces, unlike a region
   * that enters the DOM already containing its text.
   */
  protected readonly previewAnnouncement = computed(() =>
    this.store.previewStatus() === 'generating'
      && (!this.store.preview() || !this.store.hasRenderedPreview())
      ? 'Generating preview'
      : '',
  );

  private pdfDoc: PdfJs.PDFDocumentProxy | null = null;
  private pdfDocRevision: number | null = null;
  private pdfDocGeneration: number | null = null;
  private pdfDocToken = 0;
  private pdfjsLib: typeof PdfJs | null = null;
  private renderToken = 0;
  private documentToken = 0;
  private pendingSync: {
    readonly revision: number;
    readonly generation: number;
    readonly documentToken: number;
  } | null = null;
  private lockedWidth = 0;

  /**
   * The pdfjs task currently drawing into the back buffer, if any.
   * `backCanvas()` resolves to the same hidden canvas for every render until
   * one actually promotes it, so a page turn or edit-triggered `renderPdf`
   * landing while a resize-triggered render (or vice versa) is still in
   * flight would otherwise both draw into the same 2D context. Tracking the
   * task lets a newer render cancel the older one instead of racing it.
   */
  private inFlightRenderTask: PdfJs.RenderTask | null = null;

  private async loadPdfjs(): Promise<typeof PdfJs> {
    if (this.pdfjsLib) return this.pdfjsLib;
    this.pdfjsLib = await this.pdfjsLoader();
    return this.pdfjsLib;
  }

  constructor() {
    effect(() => {
      const blob = this.store.preview();
      if (blob) {
        void this.renderPdfSafely(
          blob,
          this.store.previewRevision() ?? 0,
          this.store.previewGeneration() ?? 0,
        );
      }
    });

    effect((onCleanup) => {
      const paperEl = this.paper()?.nativeElement;
      if (!paperEl) return;
      const ro = new ResizeObserver(() => {
        if (this.pdfDoc) void this.renderCurrentPageSafely();
      });
      ro.observe(paperEl);
      onCleanup(() => ro.disconnect());
    });
  }

  prev(): void {
    this.currentPage.update(p => Math.max(1, p - 1));
    this.paper()?.nativeElement.scrollTo({ top: 0 });
    void this.renderCurrentPageSafely();
  }

  next(): void {
    this.currentPage.update(p => Math.min(this.totalPages(), p + 1));
    this.paper()?.nativeElement.scrollTo({ top: 0 });
    void this.renderCurrentPageSafely();
  }

  private async renderPdf(
    blob: Blob,
    revision: number,
    generation: number,
    documentToken: number,
  ): Promise<void> {
    const pdfjs = await this.loadPdfjs();
    if (documentToken !== this.documentToken) return;
    const buffer = await blob.arrayBuffer();
    if (documentToken !== this.documentToken) return;
    // A replacement document (regeneration after an edit) should keep the
    // page the user is currently reading rather than yanking them back to
    // page 1 — only the very first document, opening into an empty preview,
    // gets the page-1 default. Capture the distinction before overwriting
    // `pdfDoc` below.
    const isFirstDocument = this.pdfDoc === null;
    const document = await pdfjs.getDocument({ data: buffer }).promise;
    if (documentToken !== this.documentToken) {
      await document.loadingTask.destroy();
      return;
    }
    this.pdfDoc = document;
    this.pdfDocRevision = revision;
    this.pdfDocGeneration = generation;
    this.pdfDocToken = documentToken;
    this.totalPages.set(this.pdfDoc.numPages);
    if (isFirstDocument) {
      this.currentPage.set(1);
    } else {
      // The resume may have gotten shorter; clamp instead of pointing past
      // the new last page.
      this.currentPage.update(page => Math.min(page, this.pdfDoc!.numPages));
    }
    this.cdr.markForCheck();
    await this.renderCurrentPageSafely();
  }

  /**
   * Wraps `renderCurrentPage` for every fire-and-forget call site (this method,
   * the `ResizeObserver` callback, `prev()`, `next()` — none of them await the
   * returned promise). `renderCurrentPage` itself still throws a genuine render
   * failure, unchanged, so unit tests can keep asserting on that directly; this
   * wrapper is what stops that rejection from reaching nobody and surfacing
   * only as an unhandled rejection while the stale frame stays frozen on
   * screen with no indication anything went wrong. A failure for the document
   * being synchronized is also reported to the store so the save indicator
   * can distinguish a preview failure from a completed synchronization.
   */
  private async renderCurrentPageSafely(): Promise<void> {
    const revision = this.pdfDocRevision;
    const generation = this.pdfDocGeneration;
    const documentToken = this.pdfDocToken;
    try {
      await this.renderCurrentPage();
    } catch (error) {
      console.error('Failed to render preview page', error);
      if (
        revision !== null
        && generation !== null
        && this.pendingSync?.revision === revision
        && this.pendingSync.generation === generation
        && this.pendingSync.documentToken === documentToken
      ) {
        this.pendingSync = null;
        this.store.reportPreviewRenderFailure(revision, generation, error);
      }
    }
  }

  /**
   * Wraps `renderPdf` for its own fire-and-forget call site (the constructor
   * effect above, which — like every `renderCurrentPageSafely` call site —
   * never awaits the returned promise). `renderPdf` covers `loadPdfjs`, the
   * blob read and `getDocument()`, any of which can reject (a chunk-load
   * failure, a corrupt PDF, …) before a single page is ever drawn — a failure
   * mode `renderCurrentPageSafely` cannot catch, since it only wraps the
   * per-page render that runs after `renderPdf` has already succeeded. Same
   * rationale as that wrapper: nothing downstream awaits this promise either,
   * so an uncaught rejection here would otherwise reach nobody.
   */
  private async renderPdfSafely(blob: Blob, revision: number, generation: number): Promise<void> {
    const documentToken = ++this.documentToken;
    this.pendingSync = { revision, generation, documentToken };
    try {
      await this.renderPdf(blob, revision, generation, documentToken);
    } catch (error) {
      console.error('Failed to load preview PDF', error);
      if (
        this.pendingSync?.revision === revision
        && this.pendingSync.generation === generation
        && this.pendingSync.documentToken === documentToken
      ) {
        this.pendingSync = null;
        this.store.reportPreviewRenderFailure(revision, generation, error);
      }
    }
  }

  /** The canvas currently hidden from view — every render targets this one. */
  private backCanvas(): ElementRef<HTMLCanvasElement> | undefined {
    return this.frontBuffer() === 'a' ? this.canvasB() : this.canvasA();
  }

  /**
   * The canvas currently on screen — about to be demoted the moment the render
   * in progress promotes its sibling (see the geometry reset at the end of
   * `renderCurrentPage`).
   */
  private frontCanvas(): ElementRef<HTMLCanvasElement> | undefined {
    return this.frontBuffer() === 'a' ? this.canvasA() : this.canvasB();
  }

  /**
   * Cancels whatever render is currently drawing into the back canvas, if
   * any, and waits for pdfjs to actually stop before returning. The rejected
   * promise this produces is consumed here, not surfaced: the render loop
   * awaiting it (in `renderCurrentPage`, above) is the one responsible for
   * telling a genuine failure apart from an expected cancellation.
   */
  private async cancelInFlightRender(): Promise<void> {
    const task = this.inFlightRenderTask;
    if (!task) return;
    task.cancel();
    try {
      await task.promise;
    } catch {
      // Expected: `cancel()` rejects the task's own promise. We only need it
      // to have settled before the next render touches the canvas.
    }
  }

  private async renderCurrentPage(): Promise<void> {
    const paperEl = this.paper();
    const frameEl = this.frame();
    const backCanvasEl = this.backCanvas();
    if (!this.pdfDoc || !paperEl || !frameEl || !backCanvasEl) return;

    const pdfDoc = this.pdfDoc;
    const documentRevision = this.pdfDocRevision;
    const documentGeneration = this.pdfDocGeneration;
    const pdfDocToken = this.pdfDocToken;
    const token = ++this.renderToken;

    // A prior render may still be drawing into this same back canvas (it only
    // ever flips to a different one on a successful promotion). Ask pdfjs to
    // stop and wait for that to actually settle before this call touches the
    // canvas — `cancel()` only requests a stop; the in-flight `render()` call
    // can still issue a few more draw operations until its promise rejects.
    await this.cancelInFlightRender();
    // Re-check after the await above: teardown (ngOnDestroy bumps the token
    // and nulls `pdfDoc`) or a still-newer render could have landed while
    // this call was waiting for the previous one to actually stop.
    if (token !== this.renderToken || this.pdfDoc !== pdfDoc) return;

    const page = await pdfDoc.getPage(this.currentPage());
    if (token !== this.renderToken || this.pdfDoc !== pdfDoc) return;

    const natural = page.getViewport({ scale: 1 });

    // Keep the card from ever getting narrower than the width at which the page
    // perfectly fills its height. While the card is wider than that, it keeps
    // its flex share and the fill-by-width page is taller and scrolls — the
    // original behaviour, untouched. Once it would get narrower, the card locks
    // to the page's aspect ratio (height is fixed by the row, so the size is
    // static) and the page fills it exactly with no shrinking and no crop.
    this.lockCardWidth(paperEl.nativeElement, natural.width / natural.height);

    // Fill the card width (the scrollbar is hidden, so no width is reserved).
    const width = paperEl.nativeElement.clientWidth;
    if (width <= 0) return;

    const fit = width / natural.width;
    const dpr = window.devicePixelRatio || 1;
    const viewport = page.getViewport({ scale: fit * dpr });

    // Render into the BACK buffer only. `canvas.width =` clears its pixel
    // buffer to transparent and `page.render()` resolves asynchronously —
    // doing this to the canvas presently on screen is exactly what produces
    // the flicker (#456 §2). The front buffer is never touched until the new
    // frame is fully painted below.
    const canvas = backCanvasEl.nativeElement;
    canvas.width = viewport.width;
    canvas.height = viewport.height;
    canvas.style.width = `${natural.width * fit}px`;
    canvas.style.height = `${natural.height * fit}px`;
    // Reset THIS canvas's own opacity to 0, in an inline style that overrides
    // the CSS resting value of 1 (see preview.css). It stays hidden while
    // being drawn into regardless — it is still the back buffer, stacked
    // below the fully opaque front one — but this is also the state the
    // eventual promotion below fades in FROM. Doing it unconditionally on
    // every render (not just the first) is what keeps the crossfade honest on
    // every subsequent page turn or regeneration, not only the first paint.
    canvas.style.opacity = '0';

    const renderTask = page.render({ canvas, viewport });
    this.inFlightRenderTask = renderTask;
    try {
      await renderTask.promise;
    } catch (error) {
      // A newer render calling `cancelInFlightRender()` above is the expected
      // way this promise rejects — pdfjs signals a cancelled task with a
      // RenderingCancelledException, and that outcome is benign: the newer
      // render is already taking over the same canvas. Anything else is a
      // genuine render failure (a corrupt PDF, a pdfjs internal error, …) and
      // must not be swallowed alongside it.
      if (this.pdfjsLib && error instanceof this.pdfjsLib.RenderingCancelledException) {
        return;
      }
      throw error;
    } finally {
      if (this.inFlightRenderTask === renderTask) this.inFlightRenderTask = null;
    }
    // Bail if torn down (ngOnDestroy bumps the token) or superseded by a
    // newer render that has already started drawing into this same back
    // canvas (see `cancelInFlightRender` above) — swapping buffers or
    // resizing the wrapper here would touch state that no longer belongs to
    // this render. A superseded render normally never reaches this line at
    // all (it rejects with RenderingCancelledException and returns above),
    // but the token is still the authoritative check: it also covers
    // teardown landing in the gap between `render().promise` resolving and
    // this line.
    if (token !== this.renderToken) return;

    // The wrapper — not either canvas — owns #paper's scroll extent, so it is
    // the one that has to grow/shrink to the new page size. Resizing it can
    // shift the viewport's scroll offset (the browser clamps `scrollTop` to
    // the new `scrollHeight`), so capture/restore around the resize rather
    // than let it drift; this is also what makes an explicit page turn (which
    // already scrolled to top before calling in here) land at the top with no
    // special-casing. Each canvas keeps its own explicit CSS width/height
    // (set above, never a percentage of the wrapper) precisely so that
    // resizing the wrapper cannot visually squash whichever frame is still on
    // screen — only the scroll extent changes, no canvas's rendered size.
    const scrollTop = paperEl.nativeElement.scrollTop;
    frameEl.nativeElement.style.width = `${natural.width * fit}px`;
    frameEl.nativeElement.style.height = `${natural.height * fit}px`;
    paperEl.nativeElement.scrollTop = scrollTop;

    // The outgoing canvas keeps its OWN opacity at 1 throughout (see below),
    // but a resize can land here with the frame having genuinely shrunk since
    // that canvas's CSS width/height was last set — a smaller frame from a
    // shrinking window, not the "same-sized" case this used to assume always
    // held. Left alone, its stale, larger geometry would stick out past the
    // new frame in both directions #frame doesn't clip (only #paper does, and
    // only while `scrollable()` is true) — a ghost of the previous, bigger
    // render, and taller-than-real `#paper` scroll extent to go with it. Snap
    // it to the exact size the incoming canvas is about to occupy — the two
    // are fully co-located for the rest of this canvas's life as the back
    // buffer, so this can never be seen as a jump. Do this unconditionally,
    // not only when the size actually changed: it costs nothing and removes
    // a size-comparison branch that would otherwise have to be kept in sync
    // with `lockCardWidth`'s own rounding.
    const demotedCanvasEl = this.frontCanvas()?.nativeElement;
    if (demotedCanvasEl) {
      demotedCanvasEl.style.width = `${natural.width * fit}px`;
      demotedCanvasEl.style.height = `${natural.height * fit}px`;
    }

    // Both frames are now fully painted — only now do we flip which one is on
    // top. Clearing the inline opacity set above lets this canvas fall back
    // to the CSS resting value of 1 (preview.css), animated by the class's
    // `transition: opacity` (disabled under prefers-reduced-motion) — a
    // crossfade in from 0 to 1, stacked ABOVE the outgoing canvas via
    // `.fb-preview-buffer-front`'s z-index. The outgoing canvas's own opacity
    // is never touched here: it stays fully opaque underneath for the whole
    // transition, which is what keeps composite alpha at 1 throughout instead
    // of letting `--color-card` bleed through a concurrent fade-out (#456
    // §2). Because every unchanged pixel between the two frames is identical,
    // only the genuinely changed region reads as moving.
    canvas.style.removeProperty('opacity');
    this.frontBuffer.update(buffer => (buffer === 'a' ? 'b' : 'a'));
    this.cdr.markForCheck();
    if (
      documentRevision !== null
      && documentGeneration !== null
      && this.pendingSync?.revision === documentRevision
      && this.pendingSync.generation === documentGeneration
      && this.pendingSync.documentToken === pdfDocToken
    ) {
      this.pendingSync = null;
      this.store.confirmPreviewRendered(documentRevision, documentGeneration);
    }
  }

  // Pin the card's minimum width to the width the page needs to fill the
  // available height (page aspect = width / height). Flexbox keeps the card at
  // its half-share while that share is wider; once it would shrink past this
  // point the min-width holds the card static (the row height is fixed, so the
  // value is stable) and the editor absorbs the remaining space instead.
  private lockCardWidth(paperEl: HTMLDivElement, aspect: number): void {
    if (!this.lockWidth()) {
      // Sole pane: the card already owns the full row, so there is nothing to
      // protect it from — and a min-width here would overflow the viewport.
      // The page fills the width and any extra height scrolls.
      this.host.nativeElement.style.removeProperty('min-width');
      this.lockedWidth = 0;
      this.scrollable.set(true);
      return;
    }
    const height = paperEl.clientHeight;
    if (height <= 0) return;
    const minWidth = Math.round(height * aspect);
    if (minWidth !== this.lockedWidth) {
      this.lockedWidth = minWidth;
      this.host.nativeElement.style.minWidth = `${minWidth}px`;
    }
    // The card is "static" once the flex row has clamped it to this min-width
    // (the editor gave up the width): the page then fills the card exactly, so
    // there is nothing to scroll and sub-pixel rounding must not let it nudge.
    // Only the taller-than-card state, where the card keeps its flex share,
    // scrolls — that behaviour is unchanged.
    this.scrollable.set(this.host.nativeElement.clientWidth > minWidth + 0.5);
  }

  ngOnDestroy(): void {
    // Bump the token first so a render already past its `await` and about to
    // recheck `token !== this.renderToken` bails out instead of touching a
    // document that is being torn down. The mobile editor<->preview toggle
    // (#425) destroys and recreates this component on every tap, so this
    // path runs far more often than the old resize-triggered mount/unmount.
    this.renderToken++;
    this.documentToken++;
    this.pendingSync = null;
    this.inFlightRenderTask?.cancel();
    void this.pdfDoc?.loadingTask?.destroy();
    this.pdfDoc = null;
    this.pdfDocRevision = null;
    this.pdfDocGeneration = null;
    this.pdfDocToken = 0;
  }
}
