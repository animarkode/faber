import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Home } from './home';

describe('Home', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Home],
      providers: [
        provideRouter([]),
      ],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(Home);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should lead to the resumes, whose guard handles signing in', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();

    const hrefs = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a')).map((a) => a.getAttribute('href'));
    expect(hrefs).toContain('/resumes');
  });

  it('should confirm a deleted account politely', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([{ path: '', component: Home }])] });
    const harness = await RouterTestingHarness.create('/?accountDeleted=true');

    const status = (harness.routeNativeElement as HTMLElement).querySelector('[role="status"]');
    expect(status?.textContent).toContain('Your account has been deleted.');
  });

  it('should show no notice for an ordinary visit', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([{ path: '', component: Home }])] });
    const harness = await RouterTestingHarness.create('/');

    expect((harness.routeNativeElement as HTMLElement).querySelector('[role="status"]')).toBeNull();
  });
});
