import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';

import { ResumesStore } from './resumes-store';
import { Resume } from './resume-response';
import { UpdatePersonRequest } from './builder/person/update-person-request';
import { API_ROUTES } from '../../core/routes/api-routes';

const RESUME_ID = 'resume-1';
const DEBOUNCE_MS = 2000;
// One flushed Retry-After: 1 plus slack, so a retry is re-issued after each advance.
const RETRY_WINDOW_MS = 1500;
// "hi" — smallest valid base64 that base64ToBlob can decode.
const PDF_BASE64 = 'aGk=';

function resume(overrides: Partial<Resume> = {}): Resume {
  return {
    id: RESUME_ID,
    createdAt: '2026-01-01T00:00:00Z',
    person: null,
    title: null,
    summary: '',
    localization: 'en-us',
    hobbies: '',
    experience: [],
    educations: [],
    courses: [],
    projects: [],
    links: [],
    skills: [],
    languages: [],
    ...overrides,
  };
}

function personRequest(): UpdatePersonRequest {
  return {
    jobTitle: 'Engineer',
    firstname: 'Ada',
    lastname: 'Lovelace',
    email: 'ada@example.com',
    phone: '123',
    country: 'UK',
    city: 'London',
    street: 'Main',
    postCode: 'E1',
    nationality: 'British',
    dateOfBirth: null,
    drivingLicense: '',
  };
}

function flushRateLimited(req: TestRequest): void {
  req.flush(
    { title: 'Too Many Requests', detail: 'Rate limit exceeded. Please retry later.' },
    { status: 429, statusText: 'Too Many Requests', headers: { 'Retry-After': '1' } },
  );
}

describe('ResumesStore projects', () => {
  let store: ResumesStore;
  let httpMock: HttpTestingController;

  const project = {
    id: 'project-1', order: 0, tagline: 'SaaS resume builder', name: 'Faber', url: 'https://faber.example',
    startDate: '2025-01-01', endDate: '', description: '<p>Resume builder</p>',
  };

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [ResumesStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(ResumesStore);
    httpMock = TestBed.inject(HttpTestingController);
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume({ projects: [project] }));
  });

  afterEach(() => {
    httpMock.verify();
    vi.useRealTimers();
  });

  it('adds a project only after the server returns it', () => {
    store.addProject();
    expect(store.projects()).toEqual([project]);

    httpMock.expectOne(API_ROUTES.resumes.projects.root(RESUME_ID)).flush({
      ...project,
      id: 'project-2',
      order: 1,
    });
    expect(store.projects()).toHaveLength(2);
  });

  it('updates a project only after the debounced server request succeeds', () => {
    store.updateProject({ ...project, name: 'Faber CV' });
    expect(store.projects()[0].name).toBe('Faber');

    vi.advanceTimersByTime(DEBOUNCE_MS);
    const request = httpMock.expectOne(API_ROUTES.resumes.projects.byId(RESUME_ID, project.id));
    expect(request.request.body).toMatchObject({ name: 'Faber CV' });
    request.flush(null);

    expect(store.projects()[0].name).toBe('Faber CV');
  });
});

describe('ResumesStore rate-limit resilience', () => {
  let store: ResumesStore;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [ResumesStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(ResumesStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    vi.useRealTimers();
  });

  function loadAndEnablePreview(initial: Resume = resume()): void {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(initial);
    store.setPreviewEnabled(true);
  }

  async function exhaustRateLimit(url: string): Promise<void> {
    for (let attempt = 0; attempt < 3; attempt++) {
      flushRateLimited(httpMock.expectOne(url));
      await vi.advanceTimersByTimeAsync(RETRY_WINDOW_MS);
    }
  }

  describe('preview pipeline', () => {
    it('retries a rate-limited generate and renders the eventual PDF', async () => {
      loadAndEnablePreview();

      flushRateLimited(httpMock.expectOne(API_ROUTES.resumes.generate(RESUME_ID)));
      await vi.advanceTimersByTimeAsync(RETRY_WINDOW_MS);

      httpMock
        .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
        .flush({ base64Pdf: PDF_BASE64 });

      expect(store.preview()).not.toBeNull();
      expect(store.error()).toBeNull();
    });

    it('keeps generating previews after a rate limit was never lifted', async () => {
      loadAndEnablePreview();
      await exhaustRateLimit(API_ROUTES.resumes.generate(RESUME_ID));

      expect(store.error()).not.toBeNull();

      store.refreshPreview();

      httpMock
        .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
        .flush({ base64Pdf: PDF_BASE64 });

      expect(store.preview()).not.toBeNull();
    });

    it('keeps generating previews after a non-429 failure', async () => {
      loadAndEnablePreview();

      httpMock
        .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
        .flush(null, { status: 500, statusText: 'Server Error' });

      store.refreshPreview();

      httpMock
        .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
        .flush({ base64Pdf: PDF_BASE64 });

      expect(store.preview()).not.toBeNull();
    });
  });

  describe('person autosave pipeline', () => {
    it('publishes the first formed title only after the person create is confirmed', async () => {
      store.loadResume(RESUME_ID);
      httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

      store.updatePerson(personRequest());
      expect(store.title()).toBe('');

      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
      httpMock
        .expectOne(API_ROUTES.resumes.persons.root(RESUME_ID))
        .flush({ ...personRequest(), id: 'person-1', dateOfBirth: '' });

      expect(store.title()).toBe('Ada Lovelace');
    });

    it('keeps the title formed by the confirmed request when a newer name edit is pending', async () => {
      store.loadResume(RESUME_ID);
      httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

      store.updatePerson(personRequest());
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
      const firstSave = httpMock.expectOne(API_ROUTES.resumes.persons.root(RESUME_ID));

      store.updatePerson({ ...personRequest(), firstname: 'Grace', lastname: 'Hopper' });
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
      firstSave.flush({ ...personRequest(), id: 'person-1', dateOfBirth: '' });

      expect(store.title()).toBe('Ada Lovelace');

      httpMock
        .expectOne(API_ROUTES.resumes.persons.byId(RESUME_ID, 'person-1'))
        .flush(null);
    });

    it('retries a rate-limited person save and settles as saved', async () => {
      store.loadResume(RESUME_ID);
      httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

      store.updatePerson(personRequest());
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
      flushRateLimited(httpMock.expectOne(API_ROUTES.resumes.persons.root(RESUME_ID)));
      await vi.advanceTimersByTimeAsync(RETRY_WINDOW_MS);

      httpMock
        .expectOne(API_ROUTES.resumes.persons.root(RESUME_ID))
        .flush({ ...personRequest(), id: 'person-1', dateOfBirth: '' });

      expect(store.saveStatus()).toBe('saved');
      expect(store.person()?.id).toBe('person-1');
    });

    it('keeps autosaving after a rate limit was never lifted', async () => {
      store.loadResume(RESUME_ID);
      httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

      store.updatePerson(personRequest());
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
      await exhaustRateLimit(API_ROUTES.resumes.persons.root(RESUME_ID));

      expect(store.saveStatus()).toBe('error');

      store.updatePerson({ ...personRequest(), firstname: 'Grace' });
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
      const retried = httpMock.expectOne(API_ROUTES.resumes.persons.root(RESUME_ID));
      expect(retried.request.body).toMatchObject({ firstname: 'Grace' });
      retried.flush({ ...personRequest(), id: 'person-2', dateOfBirth: '' });

      expect(store.person()?.id).toBe('person-2');
    });

    it('keeps autosaving after a non-429 failure', async () => {
      store.loadResume(RESUME_ID);
      httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

      store.updatePerson(personRequest());
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
      httpMock
        .expectOne(API_ROUTES.resumes.persons.root(RESUME_ID))
        .flush(null, { status: 500, statusText: 'Server Error' });

      store.updatePerson(personRequest());
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
      httpMock
        .expectOne(API_ROUTES.resumes.persons.root(RESUME_ID))
        .flush({ ...personRequest(), id: 'person-3', dateOfBirth: '' });

      expect(store.person()?.id).toBe('person-3');
    });
  });

  describe('one-off mutations', () => {
    it('publishes a manually edited title only after the save is confirmed', () => {
      store.loadResume(RESUME_ID);
      httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume({ title: 'Original' }));

      store.updateTitle('Confirmed');

      expect(store.title()).toBe('Original');
      httpMock.expectOne(API_ROUTES.resumes.title(RESUME_ID)).flush(null);
      expect(store.title()).toBe('Confirmed');
    });

    it('retries a rate-limited summary save', async () => {
      store.loadResume(RESUME_ID);
      httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

      store.updateSummary('<p>hello</p>');
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
      flushRateLimited(httpMock.expectOne(API_ROUTES.resumes.summary(RESUME_ID)));
      await vi.advanceTimersByTimeAsync(RETRY_WINDOW_MS);

      httpMock.expectOne(API_ROUTES.resumes.summary(RESUME_ID)).flush(null);

      expect(store.saveStatus()).toBe('saved');
      expect(store.error()).toBeNull();
    });

    it('reports rate-limit wording once the retries are exhausted', async () => {
      store.loadResume(RESUME_ID);
      httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

      store.updateSummary('<p>hello</p>');
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
      await exhaustRateLimit(API_ROUTES.resumes.summary(RESUME_ID));

      expect(store.error()).toContain('slow down');
      expect(store.saveStatus()).toBe('error');
    });

    it('keeps the caller message for a non-429 failure', async () => {
      store.loadResume(RESUME_ID);
      httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

      store.updateSummary('<p>hello</p>');
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
      httpMock
        .expectOne(API_ROUTES.resumes.summary(RESUME_ID))
        .flush(null, { status: 500, statusText: 'Server Error' });

      expect(store.error()).toBe('Failed to save summary.');
    });
  });

  describe('loadResume', () => {
    it('retries a rate-limited load', async () => {
      store.loadResume(RESUME_ID);

      flushRateLimited(httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)));
      await vi.advanceTimersByTimeAsync(RETRY_WINDOW_MS);

      httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

      expect(store.resume()?.id).toBe(RESUME_ID);
      expect(store.isLoading()).toBe(false);
      expect(store.error()).toBeNull();
    });
  });

  describe('notice variant', () => {
    it('starts as error', () => {
      expect(store.noticeVariant()).toBe('error');
    });

    it('switches to warning when a rate limit is reported', async () => {
      store.loadResume(RESUME_ID);
      httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

      store.updateSummary('<p>hello</p>');
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
      await exhaustRateLimit(API_ROUTES.resumes.summary(RESUME_ID));

      expect(store.noticeVariant()).toBe('warning');
    });

    it('stays error for a non-429 failure', async () => {
      store.loadResume(RESUME_ID);
      httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

      store.updateSummary('<p>hello</p>');
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
      httpMock
        .expectOne(API_ROUTES.resumes.summary(RESUME_ID))
        .flush(null, { status: 500, statusText: 'Server Error' });

      expect(store.noticeVariant()).toBe('error');
    });

    it('resets to error when the notice is cleared', async () => {
      store.loadResume(RESUME_ID);
      httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

      store.updateSummary('<p>hello</p>');
      await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
      await exhaustRateLimit(API_ROUTES.resumes.summary(RESUME_ID));
      store.clearError();

      expect(store.noticeVariant()).toBe('error');
      expect(store.error()).toBeNull();
    });
  });
});

describe('ResumesStore previewStatus (#456 §1)', () => {
  let store: ResumesStore;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [ResumesStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(ResumesStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    vi.useRealTimers();
  });

  it('starts generating immediately when the pane is enabled after load', () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());
    store.setPreviewEnabled(true);

    expect(store.previewStatus()).toBe('generating');
    httpMock
      .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
      .flush({ base64Pdf: PDF_BASE64 });
  });

  it('stays generating after the PDF arrives and becomes ready only after it is painted', () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());
    store.setPreviewEnabled(true);

    httpMock
      .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
      .flush({ base64Pdf: PDF_BASE64 });

    expect(store.previewStatus()).toBe('generating');
    expect(store.saveStatus()).toBe('saving');

    store.confirmPreviewRendered(store.previewRevision()!, store.previewGeneration()!);

    expect(store.previewStatus()).toBe('ready');
    expect(store.saveStatus()).toBe('saved');
  });

  it('returns to saving for a manual refresh until the replacement frame is painted', () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());
    store.setPreviewEnabled(true);
    httpMock
      .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
      .flush({ base64Pdf: PDF_BASE64 });
    store.confirmPreviewRendered(store.previewRevision()!, store.previewGeneration()!);
    expect(store.saveStatus()).toBe('saved');

    store.refreshPreview();

    expect(store.previewStatus()).toBe('generating');
    expect(store.saveStatus()).toBe('saving');
    httpMock
      .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
      .flush({ base64Pdf: PDF_BASE64 });
    expect(store.saveStatus()).toBe('saving');

    store.confirmPreviewRendered(store.previewRevision()!, store.previewGeneration()!);

    expect(store.previewStatus()).toBe('ready');
    expect(store.saveStatus()).toBe('saved');
  });

  it('rejects a paint acknowledgement from a superseded same-revision refresh', () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());
    store.setPreviewEnabled(true);
    httpMock
      .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
      .flush({ base64Pdf: PDF_BASE64 });
    store.confirmPreviewRendered(store.previewRevision()!, store.previewGeneration()!);

    store.refreshPreview();
    httpMock
      .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
      .flush({ base64Pdf: PDF_BASE64 });
    const supersededGeneration = store.previewGeneration()!;

    store.refreshPreview();
    const latestGeneration = supersededGeneration + 1;
    store.confirmPreviewRendered(store.previewRevision()!, supersededGeneration);

    expect(store.previewStatus()).toBe('generating');
    expect(store.saveStatus()).toBe('saving');

    httpMock
      .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
      .flush({ base64Pdf: PDF_BASE64 });
    expect(store.previewGeneration()).toBe(latestGeneration);
    store.confirmPreviewRendered(store.previewRevision()!, latestGeneration);

    expect(store.saveStatus()).toBe('saved');
  });

  it('becomes unavailable and reports a preview sync error after a failed generate', () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());
    store.setPreviewEnabled(true);

    httpMock
      .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
      .flush(null, { status: 500, statusText: 'Server Error' });

    expect(store.previewStatus()).toBe('unavailable');
    expect(store.saveStatus()).toBe('error');
    expect(store.syncErrorKind()).toBe('preview');
  });

  it('cancels an in-flight generation and settles as saved when the pane is turned off', () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());
    store.setPreviewEnabled(true);
    const request = httpMock.expectOne(API_ROUTES.resumes.generate(RESUME_ID));
    expect(store.previewStatus()).toBe('generating');

    store.setPreviewEnabled(false);

    expect(store.previewStatus()).toBe('unavailable');
    expect(store.saveStatus()).toBe('saved');
    expect(request.cancelled).toBe(true);
  });

  it('stays unavailable and saved when load completes with the preview pane disabled', () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

    expect(store.previewStatus()).toBe('unavailable');
    expect(store.saveStatus()).toBe('saved');
    httpMock.expectNone(API_ROUTES.resumes.generate(RESUME_ID));
  });

  it('keeps a manual refresh unavailable when the pane is disabled', () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());
    store.refreshPreview();

    expect(store.previewStatus()).toBe('unavailable');
    httpMock.expectNone(API_ROUTES.resumes.generate(RESUME_ID));
  });

  it('ignores a stale paint acknowledgement after a newer edit', async () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());
    store.setPreviewEnabled(true);
    httpMock.expectOne(API_ROUTES.resumes.generate(RESUME_ID)).flush({ base64Pdf: PDF_BASE64 });
    const staleRevision = store.previewRevision()!;

    store.updateSummary('<p>newer</p>');
    store.confirmPreviewRendered(staleRevision, store.previewGeneration()!);

    expect(store.previewStatus()).toBe('generating');
    expect(store.saveStatus()).toBe('saving');
    await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
    httpMock.expectOne(API_ROUTES.resumes.summary(RESUME_ID));
  });
});

describe('ResumesStore save-status sequencing', () => {
  let store: ResumesStore;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [ResumesStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(ResumesStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    vi.useRealTimers();
  });

  it('reacts on the first edit, waits for debounce, then settles after the API', async () => {
    expect(store.saveStatus()).toBe('idle');

    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

    expect(store.saveStatus()).toBe('saved');

    store.updateSummary('<p>hello</p>');
    expect(store.saveStatus()).toBe('saving');
    httpMock.expectNone(API_ROUTES.resumes.summary(RESUME_ID));

    await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
    httpMock.expectOne(API_ROUTES.resumes.summary(RESUME_ID)).flush(null);
    expect(store.saveStatus()).toBe('saved');
  });

  it('keeps error standing through a retry of the same key and only clears on that key succeeding', async () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

    store.updateSummary('<p>hello</p>');
    await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
    httpMock
      .expectOne(API_ROUTES.resumes.summary(RESUME_ID))
      .flush(null, { status: 500, statusText: 'Server Error' });
    expect(store.saveStatus()).toBe('error');

    store.updateSummary('<p>hello again</p>');
    expect(store.saveStatus()).toBe('error');

    await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
    httpMock.expectOne(API_ROUTES.resumes.summary(RESUME_ID)).flush(null);
    expect(store.saveStatus()).toBe('saved');
  });

  it('leaves status at error when an unrelated key succeeds while a failure is outstanding', async () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

    store.updateSummary('<p>hello</p>');
    await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
    httpMock
      .expectOne(API_ROUTES.resumes.summary(RESUME_ID))
      .flush(null, { status: 500, statusText: 'Server Error' });
    expect(store.saveStatus()).toBe('error');

    store.updateHobbies('<p>reading</p>');
    await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
    httpMock.expectOne(API_ROUTES.resumes.hobbies(RESUME_ID)).flush(null);

    expect(store.saveStatus()).toBe('error');
  });

  it('stays saving until both of two concurrent different-key saves settle, then reports saved', async () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

    store.updateSummary('<p>hello</p>');
    store.updateHobbies('<p>reading</p>');
    expect(store.saveStatus()).toBe('saving');

    await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
    httpMock.expectOne(API_ROUTES.resumes.summary(RESUME_ID)).flush(null);
    expect(store.saveStatus()).toBe('saving');

    httpMock.expectOne(API_ROUTES.resumes.hobbies(RESUME_ID)).flush(null);
    expect(store.saveStatus()).toBe('saved');
  });

  it('coalesces same-key edits during debounce and sends only the latest payload', async () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

    store.updateSummary('<p>first</p>');
    store.updateSummary('<p>second</p>');
    expect(store.saveStatus()).toBe('saving');

    await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
    const requests = httpMock.match(API_ROUTES.resumes.summary(RESUME_ID));
    expect(requests.length).toBe(1);
    expect(requests[0].request.body).toEqual({ summary: '<p>second</p>' });
    requests[0].flush(null);
    expect(store.saveStatus()).toBe('saved');
  });

  it('serializes repeated immediate operations and lets the later retry clear the key error', () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

    store.addEducation();
    store.addEducation();
    const first = httpMock.expectOne(API_ROUTES.resumes.educations.root(RESUME_ID));
    first.flush(null, { status: 500, statusText: 'Server Error' });
    expect(store.saveStatus()).toBe('error');

    httpMock.expectOne(API_ROUTES.resumes.educations.root(RESUME_ID)).flush({
      id: 'edu-1',
      order: 0,
      school: '',
      degree: '',
      city: '',
      startDate: '',
      endDate: '',
      description: '',
    });

    expect(store.saveStatus()).toBe('saved');
  });

  it('keeps a newer person edit pending when an older request fails, then clears on success', async () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

    store.updatePerson(personRequest());
    await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
    const first = httpMock.expectOne(API_ROUTES.resumes.persons.root(RESUME_ID));

    // Queued behind `first`; same-key requests are serialized by the store.
    store.updatePerson({ ...personRequest(), firstname: 'Grace' });

    first.flush(null, { status: 500, statusText: 'Server Error' });
    expect(store.saveStatus()).toBe('error');

    await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
    httpMock
      .expectOne(API_ROUTES.resumes.persons.root(RESUME_ID))
      .flush({ ...personRequest(), firstname: 'Grace', id: 'person-1', dateOfBirth: '' });

    expect(store.saveStatus()).toBe('saved');
  });

  it('keeps saving through API, preview generation, and the painted-frame acknowledgement', async () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());
    store.setPreviewEnabled(true);
    httpMock
      .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
      .flush({ base64Pdf: PDF_BASE64 });
    store.confirmPreviewRendered(store.previewRevision()!, store.previewGeneration()!);
    expect(store.saveStatus()).toBe('saved');

    store.updateSummary('<p>latest</p>');
    expect(store.saveStatus()).toBe('saving');
    await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);

    httpMock.expectOne(API_ROUTES.resumes.summary(RESUME_ID)).flush(null);
    const generation = httpMock.expectOne(API_ROUTES.resumes.generate(RESUME_ID));
    expect(store.saveStatus()).toBe('saving');

    generation.flush({ base64Pdf: PDF_BASE64 });
    expect(store.saveStatus()).toBe('saving');

    store.confirmPreviewRendered(store.previewRevision()!, store.previewGeneration()!);
    expect(store.saveStatus()).toBe('saved');
  });

  it('does not let an older response report saved while a newer edit is debouncing', async () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

    store.updateSummary('<p>first</p>');
    await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
    const first = httpMock.expectOne(API_ROUTES.resumes.summary(RESUME_ID));

    store.updateSummary('<p>second</p>');
    first.flush(null);
    expect(store.saveStatus()).toBe('saving');
    httpMock.expectNone(API_ROUTES.resumes.summary(RESUME_ID));

    await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);
    const second = httpMock.expectOne(API_ROUTES.resumes.summary(RESUME_ID));
    expect(second.request.body).toEqual({ summary: '<p>second</p>' });
    second.flush(null);

    expect(store.saveStatus()).toBe('saved');
  });

  it('does not apply an old resume mutation after a different resume is loaded', () => {
    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());

    store.addEducation();
    const oldMutation = httpMock.expectOne(API_ROUTES.resumes.educations.root(RESUME_ID));

    const nextResumeId = 'resume-2';
    store.loadResume(nextResumeId);
    httpMock
      .expectOne(API_ROUTES.resumes.byId(nextResumeId))
      .flush(resume({ id: nextResumeId }));

    oldMutation.flush({
      id: 'stale-education',
      order: 0,
      school: '',
      degree: '',
      city: '',
      startDate: '',
      endDate: '',
      description: '',
    });

    expect(store.resume()?.id).toBe(nextResumeId);
    expect(store.educations()).toEqual([]);
    expect(store.saveStatus()).toBe('saved');
  });

  it('regenerates the preview after a successful structural delete', () => {
    const education = {
      id: 'edu-1',
      order: 0,
      school: 'University',
      degree: 'BSc',
      city: 'London',
      startDate: '2020-01',
      endDate: '2024-01',
      description: '',
    };
    store.loadResume(RESUME_ID);
    httpMock
      .expectOne(API_ROUTES.resumes.byId(RESUME_ID))
      .flush(resume({ educations: [education] }));
    store.setPreviewEnabled(true);
    httpMock
      .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
      .flush({ base64Pdf: PDF_BASE64 });
    store.confirmPreviewRendered(store.previewRevision()!, store.previewGeneration()!);

    store.deleteEducation(education);
    httpMock.expectOne(API_ROUTES.resumes.educations.byId(RESUME_ID, education.id)).flush(null);

    httpMock
      .expectOne(API_ROUTES.resumes.generate(RESUME_ID))
      .flush({ base64Pdf: PDF_BASE64 });
    expect(store.saveStatus()).toBe('saving');
  });

  it('ignores edits before a resume exists without creating dirty state', async () => {
    store.updatePerson(personRequest());
    await vi.advanceTimersByTimeAsync(DEBOUNCE_MS);

    expect(store.saveStatus()).toBe('idle');
    httpMock.expectNone(API_ROUTES.resumes.persons.root(RESUME_ID));
  });
});

describe('ResumesStore bulk section requests', () => {
  let store: ResumesStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ResumesStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(ResumesStore);
  });

  it('starts with no bulk section request', () => {
    expect(store.requestedAllSections()).toBeNull();
  });

  it('exposes an expand request', () => {
    store.requestAllSections('expand');
    expect(store.requestedAllSections()).toEqual({ action: 'expand' });
  });

  it('exposes a collapse request', () => {
    store.requestAllSections('collapse');
    expect(store.requestedAllSections()).toEqual({ action: 'collapse' });
  });

  it('emits a distinct value for two identical requests so consumers re-run', () => {
    store.requestAllSections('expand');
    const first = store.requestedAllSections();
    store.requestAllSections('expand');
    const second = store.requestedAllSections();
    expect(second).not.toBe(first);
  });
});

describe('ResumesStore download', () => {
  let store: ResumesStore;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ResumesStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(ResumesStore);
    httpMock = TestBed.inject(HttpTestingController);
    // jsdom's Blob is not accepted by vitest's createObjectURL shim since jsdom 30.1;
    // these tests care about the request and the revoke timing, not what the URL points at.
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:resume-pdf');

    store.loadResume(RESUME_ID);
    httpMock.expectOne(API_ROUTES.resumes.byId(RESUME_ID)).flush(resume());
    // Preview is disabled by default, so loading the resume does not issue a generate request.
  });

  afterEach(() => {
    httpMock.verify();
    vi.restoreAllMocks();
  });

  it('asks the server for the PDF when no preview blob is cached', () => {
    store.downloadPdf();

    const req = httpMock.expectOne(API_ROUTES.resumes.download(RESUME_ID));
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    expect(store.isDownloading()).toBe(true);

    req.flush(new Blob(['%PDF-1.4'], { type: 'application/pdf' }));
    expect(store.isDownloading()).toBe(false);
  });

  it('does not issue a second request while one is in flight', () => {
    store.downloadPdf();
    httpMock.expectOne(API_ROUTES.resumes.download(RESUME_ID));

    store.downloadPdf();
    httpMock.expectNone(API_ROUTES.resumes.download(RESUME_ID));
  });

  it('does not download a stale server snapshot while an edit is debouncing', () => {
    store.updateSummary('<p>dirty</p>');

    expect(store.canDownload()).toBe(false);
    store.downloadPdf();
    httpMock.expectNone(API_ROUTES.resumes.download(RESUME_ID));
  });

  it('surfaces a failure and clears the in-flight flag', () => {
    store.downloadPdf();
    httpMock
      .expectOne(API_ROUTES.resumes.download(RESUME_ID))
      .flush(null, { status: 500, statusText: 'Server Error' });

    expect(store.isDownloading()).toBe(false);
    expect(store.error()).toBe('Failed to download PDF.');
    expect(store.noticeVariant()).toBe('error');
  });

  describe('saveBlob object URL cleanup', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
      vi.restoreAllMocks();
    });

    it('defers revoking the object URL past the current task', () => {
      const revokeSpy = vi.spyOn(URL, 'revokeObjectURL');

      store.downloadPdf();
      httpMock
        .expectOne(API_ROUTES.resumes.download(RESUME_ID))
        .flush(new Blob(['%PDF-1.4'], { type: 'application/pdf' }));

      // The known cross-browser race (Safari, in-app WebViews): revoking here,
      // before any timer has run, would free the blob URL before the browser
      // has a turn to start reading it and silently kill the download.
      expect(revokeSpy).not.toHaveBeenCalled();

      vi.runAllTimers();

      expect(revokeSpy).toHaveBeenCalledTimes(1);
    });
  });
});
