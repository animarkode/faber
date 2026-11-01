import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Dialog } from '@angular/cdk/dialog';
import { Builder } from './builder';
import { NoticeVariant, PreviewStatus, ResumesStore, SaveStatus, SyncErrorKind } from '../resumes-store';
import { AuthStore } from '../../../core/auth/auth-store';
import { ThemeService } from '../../../core/services/theme';
import { Resume } from '../resume-response';
import { toastHarness } from '../../../shared/testing/toast-harness';
import { User } from '../../../core/auth/contracts/user';
import { expectNoAxeViolations } from '../../../shared/testing/axe';

// `isWideScreen()` reads this at construction, so each test picks its viewport
// branch by stubbing before TestBed creates the component.
function mockMatchMedia(matches: boolean): void {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
}

describe('Builder', () => {
  let fixture: ComponentFixture<Builder>;

  const mockStore = {
    isLoading: signal(false),
    resume: signal<Resume | null>(null),
    error: signal<string | null>(null),
    noticeVariant: signal<NoticeVariant>('error'),
    person: signal(null),
    title: signal(''),
    updateTitle: vi.fn(),
    clearError: vi.fn(),
    loadResume: vi.fn(),
    setPreviewEnabled: vi.fn(),
    isDownloading: signal(false),
    canDownload: signal(true),
    saveStatus: signal<SaveStatus>('saved'),
    syncErrorKind: signal<SyncErrorKind>(null),
    downloadPdf: vi.fn(),
    requestedSection: signal(null),
    requestSection: vi.fn(),
    requestedAllSections: signal(null),
    requestAllSections: vi.fn(),
    experiences: signal([]),
    educations: signal([]),
    skills: signal([]),
    languages: signal([]),
    links: signal([]),
    courses: signal([]),
    projects: signal([]),
    hobbies: signal(''),
    summary: signal(''),
    localization: signal(''),
    preview: signal(null),
    previewRevision: signal<number | null>(null),
    previewGeneration: signal<number | null>(null),
    previewStatus: signal<PreviewStatus>('unavailable'),
    hasRenderedPreview: signal(false),
    confirmPreviewRendered: vi.fn(),
    reportPreviewRenderFailure: vi.fn(),
  } satisfies Partial<ResumesStore>;

  const mockAuthStore = {
    loading: signal(false),
    authenticated: signal(false),
    user: signal<User | null>(null),
  };

  const mockThemeService = {
    isDark: signal(false),
    toggle: vi.fn(),
  };

  const openSheet = vi.fn();

  async function createBuilder(wide: boolean): Promise<void> {
    mockMatchMedia(wide);
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [Builder],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ResumesStore, useValue: mockStore },
        { provide: ThemeService, useValue: mockThemeService },
        { provide: Dialog, useValue: { open: openSheet } },
      ],
    })
      .overrideProvider(AuthStore, { useValue: mockAuthStore })
      .overrideProvider(ActivatedRoute, {
        useValue: { snapshot: { paramMap: { get: () => 'test-id' } } },
      })
      .compileComponents();

    fixture = TestBed.createComponent(Builder);
    await fixture.whenStable();
  }

  beforeEach(async () => {
    document.documentElement.classList.remove('dark');

    mockStore.isLoading.set(false);
    mockStore.resume.set(null);
    mockStore.error.set(null);
    mockStore.noticeVariant.set('error');
    mockThemeService.isDark.set(false);
    vi.clearAllMocks();

    await createBuilder(true);
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('loads resume on init with route id', () => {
    expect(mockStore.loadResume).toHaveBeenCalledWith('test-id');
  });

  describe('canvas shell', () => {
    it('renders .builder-canvas as root container', () => {
      fixture.detectChanges();
      const root = fixture.nativeElement.querySelector('.builder-canvas');
      expect(root).toBeTruthy();
    });
  });

  describe('theme toggle button', () => {
    beforeEach(() => {
      mockStore.isLoading.set(false);
      mockAuthStore.user.set({
        id: 'u1', firstName: 'Ada', lastName: 'Lovelace', name: 'Ada Lovelace', email: 'ada@example.com', roles: [],
      } satisfies User);
      fixture.detectChanges();
    });

    afterEach(() => {
      mockAuthStore.user.set(null);
    });

    it('renders a theme toggle button', () => {
      expect(fixture.nativeElement.querySelector('[data-testid="theme-toggle"]')).toBeTruthy();
    });

    it('has aria-label "Switch to dark mode" in light mode', () => {
      mockThemeService.isDark.set(false);
      fixture.detectChanges();
      const btn = fixture.nativeElement.querySelector('[data-testid="theme-toggle"]') as HTMLButtonElement | null;
      expect(btn?.getAttribute('aria-label')).toBe('Switch to dark mode');
    });

    it('has aria-label "Switch to light mode" in dark mode', () => {
      mockThemeService.isDark.set(true);
      fixture.detectChanges();
      const btn = fixture.nativeElement.querySelector('[data-testid="theme-toggle"]') as HTMLButtonElement | null;
      expect(btn?.getAttribute('aria-label')).toBe('Switch to light mode');
    });

    it('calls themeService.toggle() when clicked', () => {
      const btn = fixture.nativeElement.querySelector('[data-testid="theme-toggle"]') as HTMLButtonElement | null;
      btn?.click();
      expect(mockThemeService.toggle).toHaveBeenCalled();
    });

    it('is absent when store is loading', () => {
      mockStore.isLoading.set(true);
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[data-testid="theme-toggle"]')).toBeNull();
    });
  });

  describe('loading state', () => {
    it('shows spinner when loading', () => {
      mockStore.isLoading.set(true);
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('fb-spinner')).toBeTruthy();
    });
  });

  describe('top bar', () => {
    beforeEach(() => fixture.detectChanges());
    afterEach(() => mockStore.person.set(null));

    it('renders back button with aria-label "Back to resumes"', () => {
      expect(fixture.nativeElement.querySelector('[aria-label="Back to resumes"]')).toBeTruthy();
    });

    it('renders header element', () => {
      expect(fixture.nativeElement.querySelector('header')).toBeTruthy();
    });

    it('does not render the person job title (#478)', () => {
      mockStore.person.set({ jobTitle: 'Engineer' } as unknown as never);
      fixture.detectChanges();

      const header = fixture.nativeElement.querySelector('header') as HTMLElement;
      expect(header.textContent).not.toContain('Engineer');
    });
  });

  describe('rail navigation (wide screens only)', () => {
    beforeEach(() => {
      mockStore.resume.set({ id: '1' } as unknown as Resume);
      fixture.detectChanges();
    });

    it('renders 10 rail section buttons', () => {
      const buttons = fixture.nativeElement
        .querySelector('nav[aria-label="Resume sections"]')
        ?.querySelectorAll('button');
      expect(buttons?.length).toBe(10);
    });

    it('renders rail as an island card (bg-card)', () => {
      const nav = fixture.nativeElement.querySelector('nav[aria-label="Resume sections"]');
      expect(nav?.className).toContain('bg-card');
    });
  });

  describe('mobile shell (#425)', () => {
    beforeEach(async () => {
      await createBuilder(false);
      mockStore.resume.set({ id: '1' } as unknown as Resume);
      fixture.detectChanges();
    });

    it('drops the section rail so the editor gets the full band width', () => {
      expect(fixture.nativeElement.querySelector('nav[aria-label="Resume sections"]')).toBeNull();
      expect(fixture.nativeElement.querySelector('[role="group"][aria-label="Section controls"]')).toBeNull();
    });

    it('renders a bottom action bar carrying the builder actions', () => {
      const bar = fixture.nativeElement.querySelector('[data-testid="mobile-action-bar"]');
      expect(bar).toBeTruthy();
      expect(bar.querySelector('app-builder-actions')).toBeTruthy();
    });

    it('offers a sections trigger that opens the sheet', () => {
      const trigger = fixture.nativeElement.querySelector('[data-testid="open-sections"]') as HTMLButtonElement;
      expect(trigger).toBeTruthy();
      trigger.click();
      expect(openSheet).toHaveBeenCalled();
    });

    it('shows the editor and hides the preview by default', () => {
      const pane = fixture.nativeElement.querySelector('[data-testid="editor-pane"]') as HTMLElement;
      expect(pane.hidden).toBe(false);
      expect(fixture.nativeElement.querySelector('app-preview')).toBeNull();
    });

    it('swaps the editor for the preview when toggled', () => {
      const toggle = fixture.nativeElement.querySelector('[data-testid="toggle-preview"]') as HTMLButtonElement;
      expect(toggle.getAttribute('aria-pressed')).toBe('false');

      toggle.click();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('app-preview')).toBeTruthy();
      expect(
        (fixture.nativeElement.querySelector('[data-testid="toggle-preview"]') as HTMLButtonElement)
          .getAttribute('aria-pressed'),
      ).toBe('true');
      expect(mockStore.setPreviewEnabled).toHaveBeenCalledWith(true);
    });

    it('keeps the editor mounted but inert while the preview shows', () => {
      // Accordion open-state lives in the SectionList instance; unmounting the
      // editor would reset it every time the user peeked at the PDF.
      (fixture.nativeElement.querySelector('[data-testid="toggle-preview"]') as HTMLButtonElement).click();
      fixture.detectChanges();

      const pane = fixture.nativeElement.querySelector('[data-testid="editor-pane"]') as HTMLElement;
      expect(pane).toBeTruthy();
      expect(pane.hidden).toBe(true);
      // jsdom sets the `inert` IDL property but does not reflect it back to the
      // attribute (unlike a real browser), so assert on the property directly.
      expect(pane.inert).toBe(true);
      expect(fixture.nativeElement.querySelector('app-editor')).toBeTruthy();
    });

    it('has no AXE violations', async () => {
      mockAuthStore.user.set({
        id: 'u1', firstName: 'Ada', lastName: 'Lovelace', name: 'Ada Lovelace', email: 'ada@example.com', roles: [],
      } satisfies User);
      fixture.detectChanges();
      await expectNoAxeViolations(fixture);
      mockAuthStore.user.set(null);
    }, 20000);
  });

  describe('section sheet dialog wiring (#425 follow-up)', () => {
    // `ResumesStore` is provided on the resume route (app.routes.ts), not
    // root. TestBed's flat module `providers` register at the same near-root
    // injector `Dialog` resolves through by default, so that setup can't
    // reproduce the bug this guards: a wrapping host's OWN `providers` array
    // creates a real ancestor injector, exactly like the route does, that
    // `Dialog.open()` can't see unless the opener forwards it explicitly.
    @Component({
      template: '<app-builder />',
      imports: [Builder],
      providers: [{ provide: ResumesStore, useValue: mockStore }],
    })
    class HostWithRoutedStore {}

    afterEach(() => {
      TestBed.inject(Dialog).closeAll();
    });

    it('opens the section sheet without NG0201 when ResumesStore lives on an ancestor injector', async () => {
      mockMatchMedia(false);
      mockStore.resume.set({ id: '1' } as unknown as Resume);
      TestBed.resetTestingModule();

      await TestBed.configureTestingModule({
        imports: [HostWithRoutedStore],
        providers: [
          provideHttpClient(),
          provideHttpClientTesting(),
          provideRouter([]),
          { provide: ThemeService, useValue: mockThemeService },
        ],
      })
        .overrideProvider(AuthStore, { useValue: mockAuthStore })
        .overrideProvider(ActivatedRoute, {
          useValue: { snapshot: { paramMap: { get: () => 'test-id' } } },
        })
        .compileComponents();

      const hostFixture = TestBed.createComponent(HostWithRoutedStore);
      await hostFixture.whenStable();
      hostFixture.detectChanges();

      const trigger = hostFixture.nativeElement.querySelector('[data-testid="open-sections"]') as HTMLButtonElement;
      expect(trigger).toBeTruthy();

      // Without forwarding its injector, Builder.openSectionSheet() throws
      // NG0201 the instant SectionSheet tries to inject ResumesStore — the
      // overlay backdrop is already attached by then, so the user just sees
      // the screen dim with nothing on top of it.
      expect(() => trigger.click()).not.toThrow();

      await hostFixture.whenStable();
      expect(document.querySelector('app-section-sheet')).toBeTruthy();
    });
  });

  describe('resume not found', () => {
    it('shows not-found message when resume is null', () => {
      mockStore.isLoading.set(false);
      mockStore.resume.set(null);
      fixture.detectChanges();
      expect((fixture.nativeElement.textContent ?? '')).toContain('Resume not found');
    });
  });

  describe('toast', () => {
    it('shows toast when store has an error', () => {
      const toast = toastHarness(fixture);
      mockStore.error.set('Failed to load resume.');
      fixture.detectChanges();
      expect(toast.query()).toBeTruthy();
      expect(toast.text()).toContain('Failed to load resume.');
    });

    it('reflects cleared error from store', () => {
      mockStore.error.set('Some error');
      fixture.detectChanges();
      mockStore.error.set(null);
      expect(mockStore.error()).toBeNull();
    });

    it('calls store.clearError when toast is dismissed', () => {
      mockStore.error.set('Something went wrong');
      fixture.detectChanges();
      toastHarness(fixture).dismiss();
      expect(mockStore.clearError).toHaveBeenCalled();
    });

    it('styles the toast as an error by default', () => {
      mockStore.error.set('Failed to load resume.');
      fixture.detectChanges();
      expect(toastHarness(fixture).query()?.className).toContain('text-destructive-text');
    });

    it('styles a rate-limit notice as a warning', () => {
      mockStore.noticeVariant.set('warning');
      mockStore.error.set('Too many requests — slow down for a moment and try again.');
      fixture.detectChanges();
      expect(toastHarness(fixture).query()?.className).toContain('text-warning-text');
    });
  });

  describe('bulk section controls pill', () => {
    beforeEach(() => {
      mockStore.resume.set({ id: '1' } as unknown as Resume);
      fixture.detectChanges();
    });

    function pill(): HTMLElement | null {
      return fixture.nativeElement.querySelector('[role="group"][aria-label="Section controls"]');
    }

    function expandButton(): HTMLButtonElement | null {
      return fixture.nativeElement.querySelector('[aria-label="Expand all sections"]');
    }

    function collapseButton(): HTMLButtonElement | null {
      return fixture.nativeElement.querySelector('[aria-label="Collapse all sections"]');
    }

    it('renders the pill as an island card', () => {
      expect(pill()?.className).toContain('bg-card');
      expect(pill()?.className).toContain('rounded-full');
      expect(pill()?.className).toContain('border-line');
    });

    it('renders the pill above the section rail', () => {
      const nav = fixture.nativeElement.querySelector('nav[aria-label="Resume sections"]') as Node;
      expect((pill()?.compareDocumentPosition(nav) ?? 0) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    });

    it('gives both buttons an accessible name and a tooltip', () => {
      expect(expandButton()?.getAttribute('title')).toBe('Expand all sections');
      expect(collapseButton()?.getAttribute('title')).toBe('Collapse all sections');
    });

    it('requests an expand of all sections', () => {
      expandButton()?.click();
      expect(mockStore.requestAllSections).toHaveBeenCalledWith('expand');
    });

    it('requests a collapse of all sections', () => {
      collapseButton()?.click();
      expect(mockStore.requestAllSections).toHaveBeenCalledWith('collapse');
    });
  });

  describe('title editing', () => {
    beforeEach(() => {
      mockStore.resume.set({ id: '1' } as unknown as Resume);
    });

    function editButton(): HTMLButtonElement | null {
      return fixture.nativeElement.querySelector('[aria-label="Edit resume title"]');
    }

    function titleInput(): HTMLInputElement | null {
      return fixture.nativeElement.querySelector('[aria-label="Resume title"]');
    }

    it('does not save when committing an unchanged empty title (#463 regression)', () => {
      mockStore.title.set('');
      fixture.detectChanges();

      editButton()?.click();
      fixture.detectChanges();

      const input = titleInput();
      expect(input).toBeTruthy();
      expect(input?.value).toBe('');

      input?.dispatchEvent(new Event('blur'));
      fixture.detectChanges();

      expect(mockStore.updateTitle).not.toHaveBeenCalled();
    });

    it('saves the typed value when committing a changed title', () => {
      mockStore.title.set('');
      fixture.detectChanges();

      editButton()?.click();
      fixture.detectChanges();

      const input = titleInput()!;
      input.value = 'Backend Engineer CV';
      input.dispatchEvent(new Event('input'));
      input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
      fixture.detectChanges();

      expect(mockStore.updateTitle).toHaveBeenCalledWith('Backend Engineer CV');
    });

    it('focuses the title input when editing starts', () => {
      mockStore.title.set('Original Title');
      fixture.detectChanges();

      editButton()?.click();
      fixture.detectChanges();

      expect(document.activeElement).toBe(titleInput());
    });

    it('saves an empty title so the fallback can be shown', () => {
      mockStore.title.set('Original Title');
      fixture.detectChanges();

      editButton()?.click();
      fixture.detectChanges();

      const input = titleInput()!;
      input.value = '   ';
      input.dispatchEvent(new Event('input'));
      input.dispatchEvent(new Event('blur'));

      expect(mockStore.updateTitle).toHaveBeenCalledWith('');
    });

    it('reverts to the previous value and does not save on Escape', () => {
      mockStore.title.set('Original Title');
      fixture.detectChanges();

      editButton()?.click();
      fixture.detectChanges();

      const input = titleInput()!;
      input.value = 'Changed Title';
      input.dispatchEvent(new Event('input'));
      input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
      fixture.detectChanges();

      expect(mockStore.updateTitle).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).toContain('Original Title');
      expect(titleInput()).toBeNull();
    });

    it('sizes the editor from a mirror of its own text so the pill cannot resize', () => {
      mockStore.title.set('Original Title');
      fixture.detectChanges();

      editButton()?.click();
      fixture.detectChanges();

      const input = titleInput()!;
      const sizer = fixture.nativeElement.querySelector('[data-testid="title-sizer"]') as HTMLElement;

      expect(input.getAttribute('size')).toBe('1');
      expect(sizer.textContent?.trim()).toBe('Original Title');

      input.value = 'A much longer resume title';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();

      expect(sizer.textContent?.trim()).toBe('A much longer resume title');
    });

    it('does not start editing when the title text itself is clicked', () => {
      mockStore.title.set('Original Title');
      fixture.detectChanges();

      const titleText = fixture.nativeElement.querySelector('header span.truncate') as HTMLElement;
      titleText.click();
      fixture.detectChanges();

      expect(titleInput()).toBeNull();
    });

    it('shows the active-state underline only in edit mode', () => {
      mockStore.title.set('Original Title');
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('[data-testid="title-edit-underline"]')).toBeNull();

      editButton()?.click();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('[data-testid="title-edit-underline"]')).toBeTruthy();
    });
  });

  describe('accessibility', () => {
    beforeEach(() => {
      mockAuthStore.user.set({
        id: 'u1', firstName: 'Ada', lastName: 'Lovelace', name: 'Ada Lovelace', email: 'ada@example.com', roles: [],
      } satisfies User);
      mockStore.resume.set({ id: '1' } as unknown as Resume);
      fixture.detectChanges();
    });

    afterEach(() => {
      mockAuthStore.user.set(null);
      document.documentElement.classList.remove('dark');
    });

    it('has no AXE violations in light theme', async () => {
      await expectNoAxeViolations(fixture);
    }, 20000);

    // jsdom cannot compute colour, so the AXE helper disables `color-contrast`; this run
    // guards the structural rules under the dark class. Contrast itself is verified manually.
    it('has no AXE violations in dark theme', async () => {
      document.documentElement.classList.add('dark');
      fixture.detectChanges();
      await expectNoAxeViolations(fixture);
    }, 20000);
  });
});
